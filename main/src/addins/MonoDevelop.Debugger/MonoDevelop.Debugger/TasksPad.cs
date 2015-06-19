// TasksPad.cs
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

            tree.RowActivated += tree_RowActivated;
            DebuggingService.CallStackChanged += OnStackChanged;
            DebuggingService.PausedEvent += OnDebuggerPaused;
            DebuggingService.ResumedEvent += OnDebuggerResumed;
            DebuggingService.StoppedEvent += OnDebuggerStopped;
        }

        private void tree_RowActivated(object o, RowActivatedArgs args)
        {
            TreeIter selected;

            if (!tree.Selection.GetSelected(out selected))
                return;
            var task = store.GetValue(selected, (int)Columns.Object) as Task;
            if (task!=null)
            {
                //TODO:change Active Task
                UpdateTask(task);
            }
        }

        private void UpdateTask(Task activetask)
        {
            throw new NotImplementedException();
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

        void TaskSchedule(TaskScheduler[] schedulers)
        {
           
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
                var frame = DebuggingService.CurrentFrame;
                var ops = GetEvaluationOptions ();
                var val=frame.GetExpressionValue("System.Threading.Tasks.TaskScheduler.GetTaskSchedulersForDebugger()",ops);
                if (val.IsEvaluating) 
                  WaitSchedulers(val);
                else{
                    GetSchedulers(val);           
                }
            

            }
            catch (Exception ex)
            {
                LoggingService.LogInternalError(ex);
            }

            tree.ExpandAll();

            treeViewState.Load();
        }
        private void GetSchedulers(ObjectValue val)
        {
             var array=val.GetAllChildren();    
            

             if (array.Length == 1)
            {
                AppendTasks(TreeIter.Zero, array[0]);
            }
            else
            {
                foreach (var scheduler in array)
                {
                    var raw = (RawValue)scheduler.GetRawValue();
                    var id = raw.GetMemberValue("Id");
                    TreeIter iter = store.AppendValues(null, id.ToString(), "", "", scheduler, (int)Pango.Weight.Normal);
                    AppendTasks(iter, scheduler);

                }
            }                   
                      
        }


        private void WaitSchedulers(ObjectValue val)
        {

            GLib.Timeout.Add(100, () =>
            {
                if (!val.IsEvaluating)
                {
                   GetSchedulers(val);                         
                    return false;
                }
                return true;
            });
        }
        static EvaluationOptions GetEvaluationOptions()
        {
            var ops = EvaluationOptions.DefaultOptions;
            ops.AllowMethodEvaluation = true;
            ops.AllowToStringCalls = true;
            ops.AllowTargetInvoke = true;
            ops.EvaluationTimeout = 20000;
            ops.EllipsizeStrings = true;
            ops.IEnumerable = true;
            ops.MemberEvaluationTimeout = 20000;
            return ops;
        }

        private void AppendTasks(TreeIter iter, ObjectValue scheduler)
        {
            var raw=scheduler.GetRawValue();
            var tasks =(RawValueArray) ((RawValue)raw).CallMethod("GetScheduledTasksForDebugger");

            var arraytasks = tasks.ToArray();


            foreach (var task in arraytasks)
                       {
                           var rawtask = (RawValue)task;
                           string icon = null;

                           var id =rawtask.GetMemberValue("Id").ToString();
                           int weight = (int)Pango.Weight.Normal;
                           var statusraw = rawtask.GetMemberValue("Status");
                           long statusint = (long)statusraw;
                           string status="";
                                    
                           switch (statusint)
                           {
                               case 7:
                                   status = "Canceled";
                                   break;
                               case 0:
                                   status = "Created";
                                   break;
                               case 6:
                                   status = "Faulted";
                                   break;
                               case 5:
                                   status = "RanToCompletion";
                                   break;
                               case 3:
                                   status = "Running";
                                   break;
                               case 1:
                                   status = "WaitingForActivation";
                                   break;
                               case 4:
                                   status = "WaitingForChildrenToComplete";
                                   break;
                               case 2:
                                   status = "WaitingToRun";
                                   break;
                           }

                           var parentraw =(RawValue) rawtask.GetMemberValue("m_parent");
                           var parent=parentraw.GetMemberValue("Id").ToString();
                        

                           string thread = "TODO:ThreadAssignment";

                           if (iter.Equals(TreeIter.Zero))
                               store.AppendValues(icon, id, status, thread, parent, task, (int)weight);
                           else
                               store.AppendValues(iter, icon, id, status, thread, parent, task, (int)weight);

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


