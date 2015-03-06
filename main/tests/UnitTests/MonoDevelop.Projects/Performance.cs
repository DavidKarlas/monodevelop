//
// Performance.cs
//
// Author:
//       David Karlaš <david.karlas@xamarin.com>
//
// Copyright (c) 2015 Xamarin Inc. (http://xamarin.com)
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
using System;
using UnitTests;
using NUnit.Framework;
using MonoDevelop.Core.ProgressMonitoring;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.CSharpBinding;
using MonoDevelop.CSharpBinding.Tests;
using System.IO;
using System.Linq;
using MonoDevelop.Ide.Gui;
using MonoDevelop.CSharp.Completion;
using MonoDevelop.Refactoring;
using Microsoft.CodeAnalysis;
using ICSharpCode.NRefactory6.CSharp;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Collections;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.ComponentModel;

namespace MonoDevelop.Projects
{
	public class Performance : TestBase
	{

		public class ProcessInfo
		{
			public ArrayList threadInfoList = new ArrayList ();
			public int basePriority;
			public string processName;
			public int processId;
			public int handleCount;
			public long poolPagedBytes;
			public long poolNonpagedBytes;
			public long virtualBytes;
			public long virtualBytesPeak;
			public long workingSetPeak;
			public long workingSet;
			public long pageFileBytesPeak;
			public long pageFileBytes;
			public long privateBytes;
			public int mainModuleId; // used only for win9x - id is only for use with CreateToolHelp32
			public int sessionId;
		}

		public class ThreadInfo
		{
			public int threadId;
			public int processId;
			public int basePriority;
			public int currentPriority;
			public IntPtr startAddress;
			public ThreadState threadState;
#if !FEATURE_PAL
			public ThreadWaitReason threadWaitReason;
#endif // !FEATURE_PAL
		}

		public static ThreadInfo [] GetProcessInfos ()
		{
			int p = (int)Environment.OSVersion.Platform;
			if ((p == 4) || (p == 6) || (p == 128)) {
				return new ThreadInfo [0];
			}
			IntPtr handle = (IntPtr)(-1);
			GCHandle bufferHandle = new GCHandle ();
			var threadInfos = new List<ThreadInfo> ();
			Hashtable processInfos = new Hashtable ();

			int processId = Process.GetCurrentProcess ().Id;
			try {
				handle = NativeMethods.CreateToolhelp32Snapshot (NativeMethods.TH32CS_SNAPPROCESS | NativeMethods.TH32CS_SNAPTHREAD, 0);
				if (handle == (IntPtr)(-1)) throw new Win32Exception ();
				int entrySize = (int)Marshal.SizeOf (typeof(NativeMethods.WinProcessEntry));
				int bufferSize = entrySize + NativeMethods.WinProcessEntry.sizeofFileName;
				int [] buffer = new int [bufferSize / 4];
				bufferHandle = GCHandle.Alloc (buffer, GCHandleType.Pinned);
				IntPtr bufferPtr = bufferHandle.AddrOfPinnedObject ();
				Marshal.WriteInt32 (bufferPtr, bufferSize);

				HandleRef handleRef = new HandleRef (null, handle);

				if (NativeMethods.Process32First (handleRef, bufferPtr)) {
					do {
						NativeMethods.WinProcessEntry process = new NativeMethods.WinProcessEntry ();
						Marshal.PtrToStructure (bufferPtr, process);
						ProcessInfo processInfo = new ProcessInfo ();
						String name = Marshal.PtrToStringAnsi ((IntPtr)((long)bufferPtr + entrySize));
						processInfo.processName = Path.ChangeExtension (Path.GetFileName (name), null);
						processInfo.handleCount = process.cntUsage;
						processInfo.processId = process.th32ProcessID;
						processInfo.basePriority = process.pcPriClassBase;
						processInfo.mainModuleId = process.th32ModuleID;
						processInfos.Add (processInfo.processId, processInfo);
						Marshal.WriteInt32 (bufferPtr, bufferSize);
					}
					while (NativeMethods.Process32Next (handleRef, bufferPtr));
				}

				NativeMethods.WinThreadEntry thread = new NativeMethods.WinThreadEntry ();
				thread.dwSize = Marshal.SizeOf (thread);
				if (NativeMethods.Thread32First (handleRef, thread)) {
					do {
						if(processId != thread.th32OwnerProcessID)
							continue;
						ThreadInfo threadInfo = new ThreadInfo ();
						threadInfo.threadId = thread.th32ThreadID;
						threadInfo.processId = thread.th32OwnerProcessID;
						threadInfo.basePriority = thread.tpBasePri;
						threadInfo.currentPriority = thread.tpBasePri + thread.tpDeltaPri;
						threadInfos.Add (threadInfo);
					}
					while (NativeMethods.Thread32Next (handleRef, thread));
				}

				return threadInfos.ToArray ();
			} finally {
				if (bufferHandle.IsAllocated)
					bufferHandle.Free ();
				//                Debug.WriteLineIf(Process.processTracing.TraceVerbose, "Process - CloseHandle(toolhelp32 snapshot handle)");
				if (handle != (IntPtr)(-1)) CloseHandle (handle);
			}
		}

		[DllImport(ExternDll.Kernel32, ExactSpelling = true, CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
		public static extern bool CloseHandle (IntPtr handle);

		[Test]
		public void BuildConsoleProject ()
		{
			var s = Stopwatch.StartNew ();
			var timer = new System.Threading.Timer (delegate {
				Console.WriteLine ("Threads count:" + Process.GetCurrentProcess ().Threads.Count + " " +GetProcessInfos ().Length
//				                   .First (p => p.processId == processId).threadInfoList.Count
									+ " " + +s.Elapsed.TotalSeconds);
			}, null, 1000, 1000);
			string solFile = Path.Combine ("..", "..", "Main.sln");

			Console.WriteLine ("Start:" + s.Elapsed.TotalSeconds);
			WorkspaceItem item = Services.ProjectService.ReadWorkspaceItem (Util.GetMonitor (), solFile);
			Assert.IsTrue (item is Solution);
			Solution solution = (Solution)item;
			Console.WriteLine ("Loaded:" + s.Elapsed.TotalSeconds);
			using (var monitor = new NullProgressMonitor ())
				TypeSystemService.Load (solution, monitor, false);

			Console.WriteLine ("Roslyn loaded:" + s.Elapsed.TotalSeconds);
			var compilations = new List<Compilation> ();
			foreach (var p in solution.Items.OfType<DotNetProject> ()) {
				compilations.Add (TypeSystemService.GetCompilationAsync (p).Result);
			}
			Console.WriteLine ("Compilation done:" + s.Elapsed.TotalSeconds);

			var types = AppDomain.CurrentDomain.GetAssemblies ()
				.Where (a => !a.IsDynamic)
				.Where (a => a.FullName.Contains ("NRefactory6"))
				.SelectMany (a => a.GetTypes ())
				.Where (t => typeof(DiagnosticAnalyzer).IsAssignableFrom (t))
				.Where (t => !t.IsInterface
					&& !t.IsAbstract
					&& !t.ContainsGenericParameters)
				.Select (t => (DiagnosticAnalyzer)Activator.CreateInstance (t))
				.ToImmutableArray ();

			Console.WriteLine ("Reflection done:" + s.Elapsed.TotalSeconds);

			foreach (var c in compilations) {
				//Remove if to run longer test
				if (c.AssemblyName == "MonoDevelop.Ide") {
					var sw = Stopwatch.StartNew ();
					var count = c.WithAnalyzers (types).GetAllDiagnosticsAsync ().Result.Length;
					sw.Stop ();
					Console.WriteLine (c.Assembly.Name + "(" + count + ") took:" + sw.Elapsed.TotalSeconds);
				}
			}

			Console.WriteLine ("Diagnostics done:" + s.Elapsed.TotalSeconds);

			//			var project = solution.GetAllProjects ().FirstOrDefault ((p) => p.Name == "UnitTests");
			//			var sym = TypeSystemService.GetCompilationAsync (project).Result.GetTypeByMetadataName ("UnitTests.TestBase");
			//			Console.WriteLine ("Find symbol:" + s.Elapsed.TotalSeconds);
			//			var haha = sym.DeclaringSyntaxReferences;
			//			var workspace = TypeSystemService.GetWorkspace (solution);
			//			var references = ((INamedTypeSymbol)sym).FindDerivedClassesAsync (workspace.CurrentSolution).Result.ToArray ();
			//			Console.WriteLine ("Find all references(" + references.Length + "):" + s.Elapsed.TotalSeconds);

			TypeSystemService.Unload (solution);

			s.Stop ();
			timer.Dispose ();
			Console.WriteLine ("Finished:" + s.Elapsed.TotalSeconds);
		}

	}
}

