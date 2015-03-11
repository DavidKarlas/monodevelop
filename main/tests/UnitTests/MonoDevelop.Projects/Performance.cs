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
		[Test]
		public void LoadCompilationsDiagnostics ()
		{
			var s = Stopwatch.StartNew ();
			var reportStatus = new Action (delegate {
				Console.WriteLine ();
				Console.WriteLine ("Time:" + s.Elapsed.TotalSeconds);
				Console.WriteLine ("Threads:" +
								   Process.GetCurrentProcess ().Threads.Count + " " +
								   NativeMethods.GetCurrentProcessThreadsInfos ().Length);
				Console.WriteLine ("Memory:" + GC.GetTotalMemory(false)/ (1024.0 * 1024));
				Console.WriteLine ();
			});
			reportStatus ();
			var timer = new System.Threading.Timer (delegate {
				reportStatus ();
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
				//Remove if want to run longer test
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

