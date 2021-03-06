﻿using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace Meziantou.Analyzer.Rules
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class UseConfigureAwaitFixer : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RuleIdentifiers.UseConfigureAwaitFalse);

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            // In case the ArrayCreationExpressionSyntax is wrapped in an ArgumentSyntax or some other node with the same span,
            // get the innermost node for ties.
            var nodeToFix = root?.FindNode(context.Span, getInnermostNodeForTie: true);
            if (nodeToFix == null)
                return;

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use ConfigureAwait(false)",
                    ct => AddConfigureAwait(context.Document, nodeToFix, value: false, ct),
                    equivalenceKey: "Use ConfigureAwait(false)"),
                context.Diagnostics);

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use ConfigureAwait(true)",
                    ct => AddConfigureAwait(context.Document, nodeToFix, value: true, ct),
                    equivalenceKey: "Use ConfigureAwait(true)"),
                context.Diagnostics);
        }

        private static async Task<Document> AddConfigureAwait(Document document, SyntaxNode nodeToFix, bool value, CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            var generator = editor.Generator;

            if (nodeToFix is AwaitExpressionSyntax awaitSyntax)
            {
                if (awaitSyntax?.Expression != null)
                {
                    var newExpression = (ExpressionSyntax)generator.InvocationExpression(
                        generator.MemberAccessExpression(awaitSyntax.Expression, nameof(Task.ConfigureAwait)),
                        generator.LiteralExpression(value));

                    var newInvokeExpression = awaitSyntax.WithExpression(newExpression);

                    editor.ReplaceNode(nodeToFix, newInvokeExpression);
                    return editor.GetChangedDocument();
                }
            }
            else if (nodeToFix is ExpressionSyntax expressionSyntax)
            {
                var newExpression = (ExpressionSyntax)generator.InvocationExpression(
                        generator.MemberAccessExpression(expressionSyntax, nameof(Task.ConfigureAwait)),
                        generator.LiteralExpression(value));

                editor.ReplaceNode(nodeToFix, newExpression);
                return editor.GetChangedDocument();
            }

            return document;
        }
    }
}
