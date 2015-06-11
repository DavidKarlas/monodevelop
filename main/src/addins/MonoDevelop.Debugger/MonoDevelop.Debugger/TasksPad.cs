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

namespace MonoDevelop.Debugger
{
    class TasksPad : Gtk.ScrolledWindow, IPadContent
    {
        PadTreeView tree;
        TreeStore store;
        bool needsUpdate;
        IPadWindow window;

        enum Columns
        {
            Icon,
            Id,
            Status,
            ThreadAssignment,
            Parent
        }

        public TasksPad()
        {
            this.ShadowType = ShadowType.None;

            store = new TreeStore(typeof(string), typeof(string), typeof(string), typeof(string), typeof(string));
            tree = new PadTreeView(store);
            tree.RulesHint = true;
            tree.HeadersVisible = true;

            TreeViewColumn col = new TreeViewColumn();
            col = new TreeViewColumn();
            col.Title = GettextCatalog.GetString("Id");
            col.Resizable = true;
            col.PackStart(tree.TextRenderer, false);

            //TODO: Visual settings


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
           // throw new NotImplementedException();
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
        }

        public void RedrawContent()
        {
            UpdateDisplay();
        }
    }
}


