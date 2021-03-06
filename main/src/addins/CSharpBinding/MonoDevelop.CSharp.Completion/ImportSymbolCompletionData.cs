﻿//
// ImportSymbolCompletionData.cs
//
// Author:
//       Mike Krüger <mkrueger@xamarin.com>
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
using System.Linq;
using MonoDevelop.Ide.CodeCompletion;
using Microsoft.CodeAnalysis;
using GLib;
using System.Collections.Generic;
using MonoDevelop.Core;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.Components.PropertyGrid.PropertyEditors;
using MonoDevelop.Ide.Editor;
using System.Text;

namespace MonoDevelop.CSharp.Completion
{
	class ImportSymbolCompletionData : RoslynCompletionData
	{
		CSharpCompletionTextEditorExtension ext;
		INamedTypeSymbol type;
		bool useFullName;

		public override IconId Icon {
			get {
				return type.GetStockIcon ();
			}
		}

		public override int PriorityGroup { get { return int.MinValue; } }

		public ImportSymbolCompletionData (CSharpCompletionTextEditorExtension ext, INamedTypeSymbol type, bool useFullName) : base (null, type.Name)
		{
			this.ext = ext;
			this.useFullName = useFullName;
			this.type = type;
			this.DisplayFlags |= DisplayFlags.IsImportCompletion;
		}

		bool initialized = false;
		bool generateUsing, insertNamespace;

		void Initialize ()
		{
			if (initialized)
				return;
			initialized = true;
			if (type.ContainingNamespace == null) 
				return;
			generateUsing = !useFullName;
			insertNamespace = useFullName;
		}

		static string GetDefaultDisplaySelection (string description, bool isSelected)
		{
			if (!isSelected)
				return "<span foreground=\"darkgray\">" + description + "</span>";
			return description;
		}

		string displayDescription = null;
		public override string GetDisplayDescription (bool isSelected)
		{
			if (displayDescription == null) {
				Initialize ();
				if (generateUsing || insertNamespace) {
					displayDescription = string.Format (GettextCatalog.GetString ("(from '{0}')"), type.ContainingNamespace.Name);
				} else {
					displayDescription = "";
				}
			}
			return GetDefaultDisplaySelection (displayDescription, isSelected);
		}

		#region IActionCompletionData implementation

		public override void InsertCompletionText (CompletionListWindow window, ref KeyActions ka, MonoDevelop.Ide.Editor.Extension.KeyDescriptor descriptor)
		{
			Initialize ();
			var doc = ext.DocumentContext;
			using (var undo = ext.Editor.OpenUndoGroup ()) {
				string text = insertNamespace ? type.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) + "." + type.Name : type.Name;
				if (text != GetCurrentWord (window)) {
					if (window.WasShiftPressed && generateUsing) 
						text = type.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) + "." + text;
					window.CompletionWidget.SetCompletionText (window.CodeCompletionContext, GetCurrentWord (window), text);
				}

				if (!window.WasShiftPressed && generateUsing) {
					AddGlobalNamespaceImport (ext.Editor, doc, type.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat));
				}
			}
			ka |= KeyActions.Ignore;
		}

		static void AddGlobalNamespaceImport (MonoDevelop.Ide.Editor.TextEditor editor, DocumentContext context, string nsName)
		{
			var parsedDocument = context.ParsedDocument;
			var unit = parsedDocument.GetAst<SemanticModel> ();
			if (unit == null)
				return;

			int offset = SearchUsingInsertionPoint (unit.SyntaxTree.GetRoot ());

			var text = new StringBuilder ();
			text.Append ("using ");
			text.Append (nsName);
			text.Append (";");
			text.Append (editor.EolMarker);

			editor.InsertText (offset, text.ToString ());
		}

		static int SearchUsingInsertionPoint (SyntaxNode parent)
		{
			var result = 0;
			foreach (SyntaxNode node in parent.ChildNodes ()) {
				if (node.IsKind (Microsoft.CodeAnalysis.CSharp.SyntaxKind.UsingDirective)) {
					result = node.FullSpan.End;
					continue;
				}
				SyntaxTrivia last = new SyntaxTrivia ();

				foreach (var trivia in node.GetLeadingTrivia ()) {
					if (last.IsKind (Microsoft.CodeAnalysis.CSharp.SyntaxKind.SingleLineCommentTrivia)||
						last.IsKind (Microsoft.CodeAnalysis.CSharp.SyntaxKind.DefineDirectiveTrivia) || 
						last.IsKind (Microsoft.CodeAnalysis.CSharp.SyntaxKind.MultiLineCommentTrivia) || 
						last.IsKind (Microsoft.CodeAnalysis.CSharp.SyntaxKind.SingleLineDocumentationCommentTrivia))
						result = trivia.Span.End;
					last = trivia;
				}
				break;
			}
			return result;
		}
		#endregion
	}
}

