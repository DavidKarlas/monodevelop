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
using System.Collections.Generic;
using System.Threading;

namespace MonoDevelop.Debugger
{
    public class TasksPad : Gtk.ScrolledWindow, IPadContent
    {
        PadTreeView tree;
        TreeStore store;
        bool needsUpdate;
        bool isUpdating;
        IPadWindow window;
        TreeViewState treeViewState;
        Dictionary<string, ThreadInfo> threadAssignments;

        enum Columns
        {
            Icon,
            Id,
            Status,
            ThreadAssignment,
            Parent,
            Object,
            Weight,
            Task
        }
#if advancedTasksDebug      
        static TasksPad()
        {
           DebuggingService.DebugSessionStarted += DebuggingService_DebugSessionStarted;         
        }

        static void DebuggingService_DebugSessionStarted(object sender, EventArgs e)
        {
            DebuggingService.DebuggerSession.TargetThreadStarted+= setAsyncDebugging;
        }

        static void setAsyncDebugging(object sender, TargetEventArgs e)
        {
            DebuggingService.DebuggerSession.TargetThreadStarted -= setAsyncDebugging;
            var ops = GetEvaluationOptions();
            try
            {
                var temp = DebuggingService.DebuggerSession.GetProcesses()[0].GetThreads();
                foreach (var i in temp)
                {
                    if (i.Backtrace.FrameCount > 0)
                    {
                        var frame = i.Backtrace.GetFrame(0);
                        var val = frame.GetExpressionValue("global::System.Threading.Tasks.Task.s_asyncDebuggingEnabled = true;", ops);
                        var result = frame.GetExpressionValue("global::System.Threading.Tasks.Task.s_asyncDebuggingEnabled", ops);
                    }
                }
                
            }
             //can't set Async debugging
            catch(Exception ex)
            {
            }

        }
#endif
        public TasksPad()
        {
            this.ShadowType = ShadowType.None;

            store = new TreeStore(typeof(string), typeof(string), typeof(string), typeof(string), typeof(string),typeof(object),typeof(int),typeof(string));
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
            col.Reorderable = true;
            tree.AppendColumn(col);

            col = new TreeViewColumn();
            col.Title = GettextCatalog.GetString("Status");
            col.Resizable = true;
            col.PackStart(tree.TextRenderer, false);
            col.AddAttribute(tree.TextRenderer, "text", (int)Columns.Status);
            col.AddAttribute(tree.TextRenderer, "weight", (int)Columns.Weight);
            col.Reorderable = true;
            tree.AppendColumn(col);

            col = new TreeViewColumn();
            col.Title = GettextCatalog.GetString("ThreadAssignment");
            col.Resizable = true;
            col.PackStart(tree.TextRenderer, false);
            col.AddAttribute(tree.TextRenderer, "text", (int)Columns.ThreadAssignment);
            col.AddAttribute(tree.TextRenderer, "weight", (int)Columns.Weight);
            col.Reorderable = true;
            tree.AppendColumn(col);

            col = new TreeViewColumn();
            col.Title = GettextCatalog.GetString("Parent");
            col.Resizable = true;
            col.PackStart(tree.TextRenderer, false);
            col.AddAttribute(tree.TextRenderer, "text", (int)Columns.Parent);
            col.AddAttribute(tree.TextRenderer, "weight", (int)Columns.Weight);
            col.Reorderable = true;
            tree.AppendColumn(col);

            col = new TreeViewColumn();
            col.Title = GettextCatalog.GetString("Task");
            col.Resizable = true;
            col.PackStart(tree.TextRenderer, false);
            col.AddAttribute(tree.TextRenderer, "text", (int)Columns.Task);
            col.AddAttribute(tree.TextRenderer, "weight", (int)Columns.Weight);
            col.Reorderable = true;
            tree.AppendColumn(col);

            Add(tree);
            ShowAll();    
            isUpdating = false;
            threadAssignments = new Dictionary<string, ThreadInfo>();
            UpdateDisplay();

            tree.RowActivated += tree_RowActivated;
            DebuggingService.CallStackChanged += OnStackChanged;
            DebuggingService.PausedEvent += OnDebuggerPaused;
            DebuggingService.ResumedEvent += OnDebuggerResumed;
            DebuggingService.StoppedEvent += OnDebuggerStopped;
        }

        //switch Task
        private void tree_RowActivated(object o, RowActivatedArgs args)
        {
            TreeIter selected;

            if (!tree.Selection.GetSelected(out selected))
                return;
            var task = store.GetValue(selected, (int)Columns.Object) as RawValue;
            if (task!=null)
            {
                DebuggingService.CallStackChanged -= OnStackChanged;
                try
                {
                    string id=store.GetValue(selected, (int)Columns.Id) as string;
                    var selectedThread = threadAssignments[id];
                    if (selectedThread != null)
                    {
                        DebuggingService.ActiveThread = selectedThread;
                        UpdateTask(task);
                    }
                }
                finally
                {
                    DebuggingService.CallStackChanged += OnStackChanged;
                }
            }
        }

        private void UpdateTask(RawValue activetask)
        {
            TreeIter iter;
            string currentThreadId=DebuggingService.ActiveThread.Id.ToString();
            if (!store.GetIterFirst(out iter))
                return;

            do {
				var task = store.GetValue (iter, (int) Columns.Object) as RawValue;
                if (task == null)
                {
                    TreeIter child;

                    if (store.IterChildren(out child))
                    {
                        do
                        {
                            task = store.GetValue(iter, (int)Columns.Object) as RawValue;
                            var weight = task == activetask ? Pango.Weight.Bold : Pango.Weight.Normal;
                            var icon = task == activetask ? Gtk.Stock.GoForward : null;
                            store.SetValue(iter, (int)Columns.Weight, (int)weight);
                            store.SetValue(iter, (int)Columns.Icon, icon);
                        } while (store.IterNext(ref child));
                    }
                }
                else
                {
                     var weight = task == activetask ? Pango.Weight.Bold : Pango.Weight.Normal;
                     var icon = task == activetask ? Gtk.Stock.GoForward : null;
                     store.SetValue(iter, (int)Columns.Weight, (int)weight);
                     store.SetValue(iter, (int)Columns.Icon, icon);
                }


            } while (store.IterNext(ref iter));

            
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
            {
                if (!isUpdating)
                {
                    isUpdating = true;
                    Update();
                }
            }
            else
                needsUpdate = true;
        }

        void Update()
        {
            if (tree.IsRealized)
                tree.ScrollToPoint(0, 0);
            needsUpdate = false;
            treeViewState.Save();
            store.Clear();

            if (!DebuggingService.IsPaused)
            {
                isUpdating = false;
                return;
            }
            try
            {
                var frame = DebuggingService.CurrentFrame;
                var ops = GetEvaluationOptions ();
                var tasksval = frame.GetExpressionValue("System.Threading.Tasks.Task.GetActiveTasks()", ops);
 #if advancedTasksDebug   
                if (tasksval.IsEvaluating) 
                  WaitAsyncDebug(tasksval);
                else
                    GetAsyncDebug(tasksval);
#endif
                //get Tasks from schedulers
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
                isUpdating = false;
            }

        }
#if advancedTasksDebug   
        private void WaitAsyncDebug(ObjectValue val)
        {
            GLib.Timeout.Add(100, () =>
            {
                if (!val.IsEvaluating)
                {
                    GetAsyncDebug(val);
                    return false;
                }
                return true;
            });
        }
        private void GetAsyncDebug(ObjectValue tasks)
        {
            var array = tasks.GetAllChildren();
            foreach (var task in array)
            {
                var rawtask = (RawValue)task.GetRawValue();
                var id = rawtask.GetMemberValue("Id").ToString();
                string thread = "";
                if (threadAssignments.ContainsKey(id))
                    thread = threadAssignments[id].Id.ToString();
                string icon = null;
                int weight = (int) Pango.Weight.Normal;
                var statusraw = rawtask.GetMemberValue("Status");
                long statusint = (long)statusraw;
                string status = toStatus((int)statusint);
                string parent = "";
                try
                {
                    var parentraw = (RawValue)rawtask.GetMemberValue("m_parent");
                    parent = parentraw.GetMemberValue("Id").ToString();
                }
                //in case of no parent
                catch (Exception ex)
                {
                }

                string taskmethod = "";
                try
                {
                    taskmethod = (string)rawtask.GetMemberValue("DebuggerDisplayMethodDescription");

                }
                catch (Exception ex)
                {
                }
                store.AppendValues(icon, id, status, thread, parent, rawtask, (int)weight, taskmethod);
            }
        }
#endif
        private void GetSchedulers(ObjectValue val)
        {
            var array = val.GetAllChildren();
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
                    TreeIter iter = store.AppendValues(null, id.ToString(), "", "", scheduler, (int)Pango.Weight.Normal,"");
                    AppendTasks(iter, scheduler);

                }
            }
             taskThreads();
             tree.ExpandAll();
             treeViewState.Load();
             isUpdating = false;            
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
            ops.EllipsizeStrings = false;
            ops.MemberEvaluationTimeout = 20000;
            return ops;
        }

        private string toStatus(int statusint)
        {
            string status = "";
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
            return status;
        }

        private void AppendTasks(TreeIter iter, ObjectValue scheduler)
        {
            try
            {
                var raw = scheduler.GetRawValue();
                var tasks = (RawValueArray)((RawValue)raw).CallMethod("GetScheduledTasksForDebugger");
                var arraytasks = tasks.ToArray();
                var activeThreadId = DebuggingService.DebuggerSession.ActiveThread.Id.ToString();
                foreach (var task in arraytasks)
                {

                    var rawtask = (RawValue)task;
                    var id = rawtask.GetMemberValue("Id").ToString();
                    string thread = "";
                    if (threadAssignments.ContainsKey(id))
                        thread = threadAssignments[id].Id.ToString();
                    string icon = thread == activeThreadId ? Gtk.Stock.GoForward : null;
                    int weight = (int)(thread == activeThreadId ? Pango.Weight.Bold : Pango.Weight.Normal);
                    var statusraw = rawtask.GetMemberValue("Status");
                    long statusint = (long)statusraw;
                    string status = toStatus((int)statusint);
                    string parent = "";
                    try
                    {
                        var parentraw = (RawValue)rawtask.GetMemberValue("m_parent");
                        parent = parentraw.GetMemberValue("Id").ToString();
                    }
                    //in case of no parent
                    catch (Exception ex)
                    {
                    }

                    string taskmethod = "";
                    try
                    {
                       taskmethod = (string)rawtask.GetMemberValue("DebuggerDisplayMethodDescription");
                       
                    }
                    catch(Exception ex)
                    {
                    }

                    //only add the task, if it's not added by async debug api
                    TreeIter iter2;
                    bool hasfound = false;
                    if (store.GetIterFirst(out iter2))
                    {
                        do
                        {
                            var taskid = store.GetValue(iter2, (int)Columns.Id) as string;
                            if (taskid == "")
                            {
                                TreeIter child;
                                if (store.IterChildren(out child))
                                {
                                    do
                                    {
                                        taskid = store.GetValue(child, (int)Columns.Id) as string;
                                        if (taskid == id)
                                        {
                                            hasfound = true;
                                        }

                                    } while (store.IterNext(ref child));
                                }
                            }
                            else
                            {
                                if (taskid == id)
                                {

                                    hasfound = true;
                                }
                            }


                        } while (store.IterNext(ref iter2));

                    }

                        if (!hasfound)
                        {

                            if (iter.Equals(TreeIter.Zero))
                                store.AppendValues(icon, id, status, thread, parent, rawtask, (int)weight, taskmethod);
                            else
                                store.AppendValues(iter, icon, id, status, thread, parent, rawtask, (int)weight, taskmethod);
                        }
                }
            }
            catch(Exception ex)
            {
            }
        }
        //bring Tasks that runs on specific Thread
        private void taskThreads()
        {
            threadAssignments.Clear();
            var processes = DebuggingService.DebuggerSession.GetProcesses();
            foreach (var process in processes)
            {              
                var threads = process.GetThreads();             
                foreach (var thread in threads)
                {
                    var frame = DebuggingService.CurrentFrame;
                    var ops = GetEvaluationOptions();
                    try
                    {
                        if (thread.Backtrace.FrameCount > 0)
                        {                  
                                var val = thread.Backtrace.GetFrame(0).GetExpressionValue("global::System.Threading.Tasks.Task.t_currentTask", ops);
                                if (val.IsEvaluating)
                                    waitThreads(val, thread);
                                else
                                {
                                    getThreads(val, thread);
                                }   
                        }
                        
                    }catch(Exception ex)
                    {
                    }
                }
            }
        }
        
        private void waitThreads(ObjectValue val,ThreadInfo thread)
        {
            GLib.Timeout.Add(100, () =>
            {
                if (!val.IsEvaluating)
                {
                    getThreads(val,thread);
                    return false;
                }
                return true;
            });
        }

        private void getThreads(ObjectValue val,ThreadInfo thread)
        {
            string id = ""; 
            try
            {
                var raw = (RawValue)val.GetRawValue();
                id = raw.GetMemberValue("Id").ToString();
            }
            //no task on the thread
            catch (Exception ex)
            {
            }
            if (id != "")
            {
                if (!threadAssignments.ContainsKey(id))
                {
                    threadAssignments.Add(id, thread);
                }
                    bool hasfound = false;
                   //add to pad the threadid
                    TreeIter iter;
                    if (store.GetIterFirst(out iter))
                    {
                        do
                        {
                            var taskid = store.GetValue(iter, (int)Columns.Id) as string;
                            if (taskid == "")
                            {
                                TreeIter child;

                                if (store.IterChildren(out child))
                                {
                                    do
                                    {
                                        taskid = store.GetValue(iter, (int)Columns.Id) as string;
                                        var weight = DebuggingService.ActiveThread.Id == thread.Id ? Pango.Weight.Bold : Pango.Weight.Normal;
                                        var icon = DebuggingService.ActiveThread.Id == thread.Id ? Gtk.Stock.GoForward : null;
                                        store.SetValue(iter, (int)Columns.Weight, (int)weight);
                                        store.SetValue(iter, (int)Columns.Icon, icon);
                                        if (taskid == id)
                                        {
                                            store.SetValue(iter, (int)Columns.ThreadAssignment, thread.Id.ToString());
                                            hasfound = true;
                                        }

                                    } while (store.IterNext(ref child));
                                }
                            }
                            else
                            {
                                if (taskid == id)
                                {
                                  var weight = DebuggingService.ActiveThread.Id == thread.Id ? Pango.Weight.Bold : Pango.Weight.Normal;
                                  var icon = DebuggingService.ActiveThread.Id == thread.Id ? Gtk.Stock.GoForward : null;                    
                                  store.SetValue(iter, (int)Columns.ThreadAssignment, thread.Id.ToString());
                                    string status=store.GetValue(iter,(int)Columns.Status) as string;
                                  if ( status== "Running" && isBlocking(thread))
                                  {
                                      store.SetValue(iter, (int)Columns.Status, "Running(Blocked)");
                                  }
                                  hasfound = true;                        
                                  store.SetValue(iter, (int)Columns.Weight, (int)weight);
                                  store.SetValue(iter, (int)Columns.Icon, icon);
                                }
                            }


                        } while (store.IterNext(ref iter));
                    }

                    if (!hasfound)
                    {
                        try
                        {
                            var raw = (RawValue)val.GetRawValue();
                            var taskid = raw.GetMemberValue("Id").ToString();
                            string icon = thread.Id == DebuggingService.ActiveThread.Id ? Gtk.Stock.GoForward : null;
                            int weight = (int)(thread.Id == DebuggingService.ActiveThread.Id ? Pango.Weight.Bold : Pango.Weight.Normal);
                            var statusraw = raw.GetMemberValue("Status");
                            long statusint = (long)statusraw;
                            string status = toStatus((int)statusint);
                            if (status == "Running" && isBlocking(thread))
                            {
                                status = "Running(Blocked)";
                            }

                            string parent = "";
                            try
                            {
                                var parentraw = (RawValue)raw.GetMemberValue("m_parent");
                                parent = parentraw.GetMemberValue("Id").ToString();
                            }
                            catch (Exception ex)
                            {
                            }

                            string taskmethod = "";
                            try
                            {
                                taskmethod = (string)raw.GetMemberValue("DebuggerDisplayMethodDescription");
                            }
                            catch (Exception ex)
                            {
                            }

                            store.AppendValues(icon, taskid, status, thread.Id.ToString(), parent, raw, (int)weight,taskmethod);
                        }
                        //no task on the thread
                        catch (Exception ex)
                        {
                        }
                    }
                }
        }

        private bool isBlocking(ThreadInfo info)
        {
           bool blocking=false;
            //if thread's location contains one of them, it is likely blocked.
           blocking = blocking || info.Location.Contains("System.Threading.Monitor.Enter");
           blocking = blocking || info.Location.Contains("System.Threading.Monitor.Monitor_wait");
           blocking = blocking || info.Location.Contains("System.Threading.Tasks.Task.Wait");
           blocking = blocking || info.Location.Contains("System.Threading.ManualResetEventSlim.Wait");
           blocking = blocking || info.Location.Contains("System.Threading.Tasks.Task.InternalWait");
           blocking = blocking || info.Location.Contains("System.Threading.Thread.Yield");
           blocking = blocking || info.Location.Contains("System.Threading.Tasks.Task.SpinWait");
           blocking = blocking || info.Location.Contains("System.Threading.Thread.Sleep");
            return blocking;
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


