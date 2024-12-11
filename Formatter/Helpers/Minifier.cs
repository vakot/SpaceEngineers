using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Helpers.Minifier
{
    public static class Minifier
    {
        private static Dictionary<string, string> _nameMapping = new Dictionary<string, string>();

        public static SyntaxNode Minify(SyntaxNode scriptRoot)
        {
            return new IdentifierRenamer().Visit(scriptRoot);
        }

        private class IdentifierRenamer : CSharpSyntaxRewriter
        {
            private int _counter = 0;

            private string GetShortName(string originalName)
            {
                if (!_nameMapping.ContainsKey(originalName))
                {
                    _nameMapping[originalName] = "x" + _counter.ToString("D3");
                    _counter++;
                }
                return _nameMapping[originalName];
            }

            public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
            {
                var originalName = node.Identifier.Text;
                var newName = GetShortName(originalName);
                Console.WriteLine($"Renaming method declaration: {originalName} -> {newName}");
                var newNode = node.WithIdentifier(SyntaxFactory.Identifier(node.Identifier.LeadingTrivia, newName, node.Identifier.TrailingTrivia));
                return base.VisitMethodDeclaration(newNode);
            }

            public override SyntaxNode VisitPropertyDeclaration(PropertyDeclarationSyntax node)
            {
                var originalName = node.Identifier.Text;
                var newName = GetShortName(originalName);
                Console.WriteLine($"Renaming property: {originalName} -> {newName}");
                var newNode = node.WithIdentifier(SyntaxFactory.Identifier(node.Identifier.LeadingTrivia, newName, node.Identifier.TrailingTrivia));
                return base.VisitPropertyDeclaration(newNode);
            }

            public override SyntaxNode VisitFieldDeclaration(FieldDeclarationSyntax node)
            {
                var newDeclarators = node.Declaration.Variables.Select(variable =>
                {
                    var originalName = variable.Identifier.Text;
                    var newName = GetShortName(originalName);
                    Console.WriteLine($"Renaming field: {originalName} -> {newName}");
                    return variable.WithIdentifier(SyntaxFactory.Identifier(variable.Identifier.LeadingTrivia, newName, variable.Identifier.TrailingTrivia));
                });

                var newDeclaration = node.Declaration.WithVariables(SyntaxFactory.SeparatedList(newDeclarators));
                var newNode = node.WithDeclaration(newDeclaration);
                return base.VisitFieldDeclaration(newNode);
            }

            public override SyntaxNode VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
            {
                var updatedExpression = (ExpressionSyntax)Visit(node.Expression);

                if (node.Name is IdentifierNameSyntax memberName && _nameMapping.TryGetValue(memberName.Identifier.Text, out var newName))
                {
                    Console.WriteLine($"Renaming member method call: {memberName.Identifier.Text} -> {newName}");
                    var updatedName = SyntaxFactory.IdentifierName(newName);
                    return node.WithExpression(updatedExpression).WithName(updatedName);
                }

                return node.WithExpression(updatedExpression).WithName(node.Name);
            }

            public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
            {
                if (node.Expression is IdentifierNameSyntax identifier)
                {
                    if (_nameMapping.TryGetValue(identifier.Identifier.Text, out var newName))
                    {
                        Console.WriteLine($"Renaming method call: {identifier.Identifier.Text} -> {newName}");
                        var updatedIdentifier = identifier.WithIdentifier(
                            SyntaxFactory.Identifier(identifier.Identifier.LeadingTrivia, newName, identifier.Identifier.TrailingTrivia));
                        var updatedNode = node.WithExpression(updatedIdentifier);
                        return base.VisitInvocationExpression(updatedNode);
                    }
                }
                else if (node.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    if (memberAccess.Name is IdentifierNameSyntax memberName &&
                        _nameMapping.TryGetValue(memberName.Identifier.Text, out var newName))
                    {
                        Console.WriteLine($"Renaming member method call: {memberName.Identifier.Text} -> {newName}");
                        var updatedMemberName = memberName.WithIdentifier(
                            SyntaxFactory.Identifier(memberName.Identifier.LeadingTrivia, newName, memberName.Identifier.TrailingTrivia));
                        var updatedMemberAccess = memberAccess.WithName(updatedMemberName);
                        var updatedNode = node.WithExpression(updatedMemberAccess);
                        return base.VisitInvocationExpression(updatedNode);
                    }
                }

                return base.VisitInvocationExpression(node);
            }

            public override SyntaxNode VisitForEachStatement(ForEachStatementSyntax node)
            {
                var originalName = node.Identifier.Text;
                var newName = GetShortName(originalName);

                var newIdentifier = SyntaxFactory.Identifier(node.Identifier.LeadingTrivia, newName, node.Identifier.TrailingTrivia);
                var updatedNode = node.WithIdentifier(newIdentifier);
                Console.WriteLine($"Renamed foreach variable: {originalName} -> {newName}");
                var updatedBody = (StatementSyntax)Visit(node.Statement);
                updatedNode = updatedNode.WithStatement(updatedBody);


                return base.VisitForEachStatement(updatedNode);
            }

            public override SyntaxNode VisitVariableDeclarator(VariableDeclaratorSyntax node)
            {
                var newName = GetShortName(node.Identifier.Text);
                var newNode = node.WithIdentifier(SyntaxFactory.Identifier(node.Identifier.LeadingTrivia, newName, node.Identifier.TrailingTrivia));
                return base.VisitVariableDeclarator(newNode);
            }

            public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
            {
                if (_nameMapping.TryGetValue(node.Identifier.Text, out var newName))
                {
                    Console.WriteLine($"Renaming method call/expression: {node.Identifier.Text} -> {newName}");
                    var updatedNode = node.WithIdentifier(SyntaxFactory.Identifier(node.Identifier.LeadingTrivia, newName, node.Identifier.TrailingTrivia));
                    return updatedNode;
                }

                return base.VisitIdentifierName(node);
            }
        }
    }
}
