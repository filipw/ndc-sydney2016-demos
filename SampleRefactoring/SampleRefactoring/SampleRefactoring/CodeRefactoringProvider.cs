using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;

namespace SampleRefactoring
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(SampleRefactoringCodeRefactoringProvider)), Shared]
    internal class SampleRefactoringCodeRefactoringProvider : CodeRefactoringProvider
    {
        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(context.Span);

            var typeDecl = node as BaseTypeDeclarationSyntax;
            if (typeDecl == null || context.Document.Name.ToLowerInvariant() == $"{typeDecl.Identifier.ToString().ToLowerInvariant()}.cs")
            {
                return;
            }

            // note: these are very simple refactorings - in real life scenarios, there are several other things we should be handling
            // i.e. namespace, using statement, naming collisions etc
            var action = CodeAction.Create("Move type to file", c => MoveToFile(root, context.Document, typeDecl));
            var action2 = CodeAction.Create("Rename type to match file name", c => RenameTypeToMatchFilenameAsync(context.Document, typeDecl, c));

            context.RegisterRefactoring(action);
            context.RegisterRefactoring(action2);
        }

        private static Task<Solution> MoveToFile(SyntaxNode root, Document document, BaseTypeDeclarationSyntax typeDecl)
        {
            var newRoot = root.RemoveNode(typeDecl, SyntaxRemoveOptions.KeepNoTrivia);
            document = document.WithSyntaxRoot(newRoot);

            var newDocument = document.Project.AddDocument($"{typeDecl.Identifier}.cs", SyntaxFactory.CompilationUnit().AddMembers(typeDecl));
            return Task.FromResult(newDocument.Project.Solution);
        }

        private async Task<Solution> RenameTypeToMatchFilenameAsync(Document document, BaseTypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var typeModel = semanticModel.GetDeclaredSymbol(typeDecl, cancellationToken);

            return await Renamer.RenameSymbolAsync(document.Project.Solution, typeModel, document.Name.Replace(".cs", ""), null,
                cancellationToken);
        }
    }
}