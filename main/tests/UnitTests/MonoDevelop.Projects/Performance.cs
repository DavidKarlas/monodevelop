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

namespace MonoDevelop.Projects
{
	public class Performance : TestBase
	{
		[Test]
		public void BuildConsoleProject ()
		{
			var s = System.Diagnostics.Stopwatch.StartNew ();
			string solFile = @"K:\GIT\MD1\monodevelop\main\Main.sln";

			Console.WriteLine ("Start:" + s.Elapsed.TotalSeconds);
			WorkspaceItem item = Services.ProjectService.ReadWorkspaceItem (Util.GetMonitor (), solFile);
			Assert.IsTrue (item is Solution);
			Solution solution = (Solution)item;
			Console.WriteLine ("Loaded:" + s.Elapsed.TotalSeconds);
			using (var monitor = new NullProgressMonitor ())
				TypeSystemService.Load (solution, monitor, false);

			Console.WriteLine ("Roslyn loaded:" + s.Elapsed.TotalSeconds);

			TypeSystemService.GetCompilationAsync ((Project)solution.Items [0]).Wait ();
			Console.WriteLine ("Compilation done:" + s.Elapsed.TotalSeconds);

			var tww = new TestWorkbenchWindow ();
			var content = new TestViewContent ();
			var fileName = @"K:\GIT\MD1\monodevelop\main\tests\UnitTests\MonoDevelop.Projects\Performance.cs";
			var text = File.ReadAllText (fileName);
			int endPos = text.IndexOf ("TestBase");

			MonoDevelopWorkspace.CreateTextLoader = delegate (string fn) {
				return MonoDevelopTextLoader.CreateFromText (text);
			};

			var project = solution.GetAllProjects ().FirstOrDefault ((p) => p.Name == "UnitTests");

			content.Project = project;

			tww.ViewContent = content;
			content.ContentName = fileName;
			content.Data.MimeType = "text/x-csharp";
			var doc = new MonoDevelop.Ide.Gui.Document (tww);
			doc.SetProject (project);

			content.Text = text;
			content.CursorPosition = Math.Max (0, endPos);

			doc.UpdateParseDocument ();

			Console.WriteLine ("Loaded file:" + s.Elapsed.TotalSeconds);

			var info = CurrentRefactoryOperationsHandler.GetSymbolInfoAsync (doc, doc.Editor.CaretOffset).Result;
			var semanticModel = doc.ParsedDocument.GetAst<Microsoft.CodeAnalysis.SemanticModel> ();
			var sym = info.Symbol ?? info.DeclaredSymbol;
			var haha = sym.DeclaringSyntaxReferences;
			var workspace = TypeSystemService.GetWorkspace (solution);
			var references = Microsoft.CodeAnalysis.FindSymbols.SymbolFinder.FindReferencesAsync (sym, workspace.CurrentSolution).Result.ToArray ();


//			{
//				var linkedSymbols = new System.Collections.Generic.HashSet<Microsoft.CodeAnalysis.ISymbol> ();
//
//				foreach (var location in sym.DeclaringSyntaxReferences) {
//					var originalDocument = workspace.CurrentSolution.GetDocument(location.SyntaxTree);
//
//					foreach (var linkedDocumentId in originalDocument.GetLinkedDocumentIds())
//					{
//						var linkedDocument = workspace.GetDocument(linkedDocumentId);
//						var linkedSyntaxRoot = linkedDocument.GetSyntaxRootAsync ().Result;
//
//						// Defend against constructed solutions with inconsistent linked documents
//						if (!linkedSyntaxRoot.FullSpan.Contains(location.Span))
//						{
//							continue;
//						}
//
//						var linkedNode = linkedSyntaxRoot.FindNode(location.Span, getInnermostNodeForTie: true);
//
//						var semanticModel2 = linkedDocument.GetSemanticModelAsync ().Result;
//						var linkedSymbol = semanticModel2.GetDeclaredSymbol (linkedNode);
//
//						if (linkedSymbol != null &&
//						    linkedSymbol.Kind == sym.Kind &&
//						    linkedSymbol.Name == sym.Name &&
//						!linkedSymbols.Contains(linkedSymbol))
//						{
//							linkedSymbols.Add(linkedSymbol);
//						}
//					}
//				}
//
//			}





			//			CSharp.Refactoring.ResolveCommandHandler.GetPossibleNamespaces (doc.Editor, doc, doc.Editor.SelectionRange);

			Console.WriteLine ("Find all references(" + references.Length + "):" + s.Elapsed.TotalSeconds);



			TypeSystemService.Unload (solution);


			s.Stop ();
			Console.WriteLine ("Finished:" + s.Elapsed.TotalSeconds);
		}
	}
}

