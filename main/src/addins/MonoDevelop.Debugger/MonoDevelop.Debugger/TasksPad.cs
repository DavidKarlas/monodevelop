﻿// TasksPad.cs
//
// Author:
//   Bence Tilk <bence.tilk@gmail.com>
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.


using Gtk;

using System;
using MonoDevelop.Core;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Components;
using Mono.Debugging.Client;
using MonoDevelop.Ide.Gui.Components;
using MonoDevelop.Ide;
using System.Threading.Tasks;
using System.Reflection;

namespace MonoDevelop.Debugger
{
    class TasksPad : Gtk.ScrolledWindow, IPadContent
    {
        PadTreeView tree;
        TreeStore store;
        bool needsUpdate;
        IPadWindow window;
        TreeViewState treeViewState;

        enum Columns
        {
            Icon,
            Id,
            Status,
            ThreadAssignment,
            Parent,
            Object,
            Weight
        }

        public TasksPad()
        {
            this.ShadowType = ShadowType.None;

            store = new TreeStore(typeof(string), typeof(string), typeof(string), typeof(string), typeof(string),typeof(object),typeof(int));
            tree = new PadTreeView(store);
            tree.RulesHint = true;
            tree.HeadersVisible = true;
            treeViewState = new TreeViewState(tree, (int)Columns.Object);

            TreeViewColumn col = new TreeViewColumn();
            CellRenderer crp = new CellRendererImage();
            col.PackStart(crp, false);
            col.AddAttribute(crp, "stock_id", (int)Columns.Icon);
            tree.AppendColumn(col);


            col = new TreeViewColumn();
            col.Title = GettextCatalog.GetString("Id");
            col.PackStart(tree.TextRenderer, false);
            col.AddAttribute(tree.TextRenderer, "text", (int)Columns.Id);
            col.AddAttribute(tree.TextRenderer, "weight", (int)Columns.Weight);
            col.Resizable = true;
            col.Alignment = 0.0f;
            tree.AppendColumn(col);

            col = new TreeViewColumn();
            col.Title = GettextCatalog.GetString("Status");
            col.Resizable = true;
            col.PackStart(tree.TextRenderer, false);
            col.AddAttribute(tree.TextRenderer, "text", (int)Columns.Status);
            col.AddAttribute(tree.TextRenderer, "weight", (int)Columns.Weight);
            tree.AppendColumn(col);

            col = new TreeViewColumn();
            col.Title = GettextCatalog.GetString("ThreadAssignment");
            col.Resizable = true;
            col.PackStart(tree.TextRenderer, false);
            col.AddAttribute(tree.TextRenderer, "text", (int)Columns.ThreadAssignment);
            col.AddAttribute(tree.TextRenderer, "weight", (int)Columns.Weight);
            tree.AppendColumn(col);

            col = new TreeViewColumn();
            col.Title = GettextCatalog.GetString("Parent");
            col.Resizable = true;
            col.PackStart(tree.TextRenderer, false);
            col.AddAttribute(tree.TextRenderer, "text", (int)Columns.Parent);
            col.AddAttribute(tree.TextRenderer, "weight", (int)Columns.Weight);
            tree.AppendColumn(col);

            Add(tree);
            ShowAll();

            UpdateDisplay();

            DebuggingService.CallStackChanged += OnStackChanged;
            DebuggingService.PausedEvent += OnDebuggerPaused;
            DebuggingService.ResumedEvent += OnDebuggerResumed;
            DebuggingService.StoppedEvent += OnDebuggerStopped;
        }
        public override void Dispose()
        {
            base.Dispose();
            DebuggingService.CallStackChanged -= OnStackChanged;
            DebuggingService.PausedEvent -= OnDebuggerPaused;
            DebuggingService.ResumedEvent -= OnDebuggerResumed;
            DebuggingService.StoppedEvent -= OnDebuggerStopped;
        }

        private void OnDebuggerStopped(object sender, EventArgs e)
        {
            UpdateDisplay();
        }

        private void OnDebuggerResumed(object sender, EventArgs e)
        {
            UpdateDisplay();
        }

        private void OnDebuggerPaused(object sender, EventArgs e)
        {
            UpdateDisplay();
        }

        private void OnStackChanged(object sender, EventArgs e)
        {
            UpdateDisplay();
        }

        public void UpdateDisplay()
        {
            if (window != null && window.ContentVisible)
                Update();
            else
                needsUpdate = true;
        }

        void Update()
        {
            if (tree.IsRealized)
                tree.ScrollToPoint(0, 0);
            treeViewState.Save();

            store.Clear();

            if (!DebuggingService.IsPaused)
                return;

            try
            {
                

                //TaskScheduler[] schedulers=TaskScheduler.GetTaskSchedulersForDebugger();
                TaskScheduler[] schedulers = (TaskScheduler[])(typeof(TaskScheduler)).GetMethod("GetTaskSchedulersForDebugger", BindingFlags.NonPublic |BindingFlags.Static).Invoke(null, null);

                
                if (schedulers.Length == 1 && schedulers[0].GetType()==typeof(Xwt.XwtTaskScheduler))
                {
                    AppendTasks(TreeIter.Zero, schedulers[0]);
                }
                else
                {
                    foreach (var scheduler in schedulers)
                    {
                        if (scheduler.GetType!=typeof(Xwt.XwtTaskScheduler))
                        {
                          TreeIter iter = store.AppendValues(null, scheduler.Id.ToString(),"","",scheduler, (int)Pango.Weight.Normal);
                          AppendTasks(iter,scheduler);
                         }
                    }
                }
               

            }
            catch (Exception ex)
            {
                LoggingService.LogInternalError(ex);
            }

            tree.ExpandAll();

            treeViewState.Load();
        }

        private void AppendTasks(TreeIter iter, TaskScheduler scheduler)
        {
           
             //var tasks=scheduler.GetScheduledTasksForDebugger();
             Task[] tasks =(Task[]) typeof(TaskScheduler).GetMethod("GetScheduledTasksForDebugger",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(scheduler, null);

             foreach (var task in tasks)
             {
                 string icon = null;
                 string id = task.Id.ToString();
                 int weight = (int)Pango.Weight.Normal;
                 string status = task.Status.ToString();


                 if (iter.Equals(TreeIter.Zero))
                     store.AppendValues(icon, id, status, "TODO:ThreadAssignment","TODO:Parent",task, (int)weight);
                 else
                     store.AppendValues(iter, icon, id, status, "TODO:ThreadAssignment", "TODO:Parent", task, (int)weight);

             }
               
        }


        public string Id
        {
            get { return "MonoDevelop.Debugger.TasksPad"; }
        }

        public string DefaultPlacement
        {
            get { return "Bottom"; }
        }

        public Widget Control
        {
            get { return this; }
        }

        public void Initialize(IPadWindow window)
        {
            this.window = window;
            window.PadContentShown+= delegate {
				if (needsUpdate)
					Update ();
			};
        }

        public void RedrawContent()
        {
            UpdateDisplay();
        }
    }
}


