using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Workspaces;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Options;
using System.Threading.Tasks;

using Helpers.Minifier;

namespace Formatter
{
    public class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: FlattenScriptGenerator --input=<inputPath> --output=<outputDirectory>");
                return;
            }

            string inputDir = args.FirstOrDefault(arg => arg.StartsWith("--input"))?.Split('=')[1];
            string outputDir = args.FirstOrDefault(arg => arg.StartsWith("--output"))?.Split('=')[1];

            if (string.IsNullOrEmpty(inputDir) || string.IsNullOrEmpty(outputDir))
            {
                Console.WriteLine("Usage: FlattenScriptGenerator --input=<inputPath> --output=<outputDirectory>");
                return;
            }

            string[] scriptFiles = Directory.GetFiles(Path.Combine(inputDir, "Scripts"), "*.cs", SearchOption.AllDirectories);
            string[] sharedFiles = Directory.GetFiles(Path.Combine(inputDir, "Shared"), "*.cs", SearchOption.AllDirectories);

            foreach (var scriptFile in scriptFiles)
            {
                string formattedScript = FlattenScript(scriptFile, sharedFiles);
                string outputFile = Path.Combine(outputDir, Path.GetFileName(scriptFile));
                File.WriteAllText(outputFile, formattedScript);

                Console.WriteLine($"Formatted -> {outputFile}");
            }
        }

        static string FlattenScript(string scriptFile, string[] sharedFiles)
        {
            // Read the content of the script file
            string scriptContent = File.ReadAllText(scriptFile);

            // Parse the script content into a Roslyn SyntaxTree
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(scriptContent);

            // Get the root of the syntax tree (the compilation unit)
            SyntaxNode root = syntaxTree.GetRoot();

            // List of methods to apply sequentially on the root
            Action<SyntaxNode>[] methods = new Action<SyntaxNode>[]
            {
                (node) => root = ResolveShared(node, sharedFiles),
                // (node) => root = Minify(node),
                (node) => root = RemoveNamespaces(node),
                (node) => root = RemoveRegionDeclarations(node),
                (node) => root = RemoveProgramDeclaration(node),
                (node) => root = RemoveUsingDeclaration(node),
                (node) => root = RemoveComments(node),
                // (node) => root = FormatHeadingCode(node),
                // (node) => root = FormatHeadingComments(node),
                (node) => root = NormalizeWhitespace(node),
            };

            // Loop through and apply all methods
            foreach (var method in methods)
            {
                method(root);
            }

            // Convert the modified SyntaxNode back to a string
            string formattedScript = root.ToFullString().Trim();

            return formattedScript;
        }


        private static SyntaxNode ResolveShared(SyntaxNode scriptRoot, string[] sharedFiles)
        {
            // Get the using directives from the current script
            var usingDirectives = scriptRoot.DescendantNodes().OfType<UsingDirectiveSyntax>().ToList();

            // Define a list to collect all the classes and members to append to the script
            var classMembersToAdd = new List<MemberDeclarationSyntax>();

            // For each using directive in the script, resolve its corresponding namespace
            foreach (var usingDirective in usingDirectives)
            {
                // Get the namespace name from the using directive (e.g., "SpaceEngineers.Shared.SurfaceContentManager")
                var namespaceName = usingDirective.Name.ToString();

                // Process each shared file to find the relevant namespace content
                foreach (var sharedFile in sharedFiles)
                {
                    // Read and parse the content of the shared file
                    var sharedFileContent = File.ReadAllText(sharedFile);
                    var sharedSyntaxTree = CSharpSyntaxTree.ParseText(sharedFileContent);
                    var sharedRoot = sharedSyntaxTree.GetRoot();

                    // Find namespace declarations in the shared file
                    var namespaceDeclarations = sharedRoot.DescendantNodes().OfType<NamespaceDeclarationSyntax>();

                    foreach (var namespaceDeclaration in namespaceDeclarations)
                    {
                        // Check if the current namespace matches the one from the using directive
                        if (namespaceDeclaration.Name.ToString() == namespaceName)
                        {
                            // Add only member declaration nodes (e.g., classes, methods, etc.) to the classMembersToAdd list
                            var members = namespaceDeclaration.Members.OfType<MemberDeclarationSyntax>();
                            classMembersToAdd.AddRange(members);
                        }
                    }
                }
            }

            // Cast the script root to CompilationUnitSyntax to add the class members at the end
            var compilationUnitRoot = (CompilationUnitSyntax)scriptRoot;

            // Add the collected class members to the end of the script
            scriptRoot = compilationUnitRoot.AddMembers(classMembersToAdd.ToArray());

            return scriptRoot;
        }

        private static SyntaxNode RemoveNamespaces(SyntaxNode scriptRoot)
        {
            // Remove all namespace declarations but keep the content inside
            var modifiedRoot = scriptRoot
                .DescendantNodesAndSelf()
                .OfType<NamespaceDeclarationSyntax>()
                .Aggregate(scriptRoot, (current, namespaceNode) =>
                {
                    // Replace the namespace declaration with its members
                    return current.ReplaceNode(namespaceNode, namespaceNode.Members);
                });

            return modifiedRoot;
        }
    
        private static SyntaxNode RemoveRegionDeclarations(SyntaxNode scriptRoot)
        {
            // First, remove all #region and #endregion directives from the tree
            var modifiedRoot = scriptRoot;

            // Remove all #region directives
            modifiedRoot = modifiedRoot
                .ReplaceTrivia(
                    modifiedRoot.DescendantTrivia()
                        .Where(trivia => trivia.IsKind(SyntaxKind.RegionDirectiveTrivia)),
                    (oldTrivia, _) => default(SyntaxTrivia));

            // Remove all #endregion directives
            modifiedRoot = modifiedRoot
                .ReplaceTrivia(
                    modifiedRoot.DescendantTrivia()
                        .Where(trivia => trivia.IsKind(SyntaxKind.EndRegionDirectiveTrivia)),
                    (oldTrivia, _) => default(SyntaxTrivia));

            return modifiedRoot;
        }

        private static SyntaxNode RemoveProgramDeclaration(SyntaxNode scriptRoot)
        {
            // Find the class declaration of the "Program" class
            var programClassDeclaration = scriptRoot.DescendantNodesAndSelf()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.Text == "Program");

            // If the "Program" class is found, remove the class declaration but keep its content (members)
            if (programClassDeclaration != null)
            {
                // Replace the class declaration with its members
                var modifiedRoot = scriptRoot.ReplaceNode(programClassDeclaration, programClassDeclaration.Members);

                return modifiedRoot;
            }

            // If the "Program" class is not found, return the original root
            return scriptRoot;
        }

        private static SyntaxNode RemoveUsingDeclaration(SyntaxNode scriptRoot)
        {
            // Cast the root to CompilationUnitSyntax to modify the using statements
            var compilationUnitRoot = (CompilationUnitSyntax)scriptRoot;

            // Remove all using directives
            var updatedRoot = compilationUnitRoot.WithUsings(SyntaxFactory.List<UsingDirectiveSyntax>());

            return updatedRoot;
        }

        private static SyntaxNode RemoveComments(SyntaxNode scriptRoot)
        {
            // Find the marker line "!!! DONT CHANGE ANYTHING BELOW THIS LINE !!!"
            var markerLine = scriptRoot.DescendantTrivia()
                .FirstOrDefault(trivia => trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) && trivia.ToString().Contains("!!! DONT CHANGE ANYTHING BELOW THIS LINE !!!"));

            // If the marker line is found
            if (markerLine != default(SyntaxTrivia))
            {
                // Get all trivia items (including comments)
                var triviaList = scriptRoot.DescendantTrivia().ToList();

                // Find the index of the marker line's trivia
                var markerIndex = triviaList.IndexOf(markerLine);

                // Get the trivia after the marker line (including the next line)
                var triviaAfterMarker = triviaList.Skip(markerIndex + 4).ToList();

                // Replace comments after the second line (including the line after the marker)
                var modifiedRoot = scriptRoot.ReplaceTrivia(
                    triviaAfterMarker.Where(trivia => trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)),
                    (oldTrivia, _) => default(SyntaxTrivia)); // Remove comment trivia

                return modifiedRoot;
            }

            // If the marker is not found, return the original root
            return scriptRoot;
        }

        private static SyntaxNode FormatHeadingComments(SyntaxNode scriptRoot)
        {
            // Convert the root to a string and split by lines
            var scriptContent = scriptRoot.ToFullString();
            var lines = scriptContent.Split(new[] { Environment.NewLine }, StringSplitOptions.None).ToList();

            // Find the line containing the marker
            int markerIndex = lines.FindIndex(line => line.Contains("!!! DONT CHANGE ANYTHING BELOW THIS LINE !!!"));

            if (markerIndex == -1)
            {
                // If the marker isn't found, return the original script
                return scriptRoot;
            }

            // Split the lines into untouched (before marker) and those to format
            var untouchedLines = lines.Take(markerIndex + 4).ToList(); // Include the marker line
            var linesToFormat = lines.Skip(markerIndex + 4).ToList();

            // Format comments in the lines that need processing
            for (int i = 0; i < untouchedLines.Count; i++)
            {
                var line = untouchedLines[i];

                // Process single-line comments
                if (line.TrimStart().StartsWith("//"))
                {
                    // Trim leading spaces before the comment and align to the left
                    untouchedLines[i] = line.TrimStart();
                }
                // Process multi-line comments
                else if (line.TrimStart().StartsWith("/*"))
                {
                    // If it's a multi-line comment, we need to handle the start and end lines
                    bool isMultiLineComment = true;
                    while (isMultiLineComment && i < untouchedLines.Count)
                    {
                        // Remove leading spaces and keep the content aligned
                        untouchedLines[i] = untouchedLines[i].TrimStart();

                        if (untouchedLines[i].StartsWith("*"))
                        {
                            untouchedLines[i] = " " + untouchedLines[i];
                        }
                        
                        // Check if it's the end of a multi-line comment
                        if (untouchedLines[i].Contains(" */"))
                        {
                            isMultiLineComment = false;
                        }
                        i++;
                    }
                }
            }

            // Rebuild the entire script content by joining the untouched and formatted parts
            var finalContent = string.Join(Environment.NewLine, untouchedLines) + Environment.NewLine + string.Join(Environment.NewLine, linesToFormat);

            // Parse the final content back into a SyntaxNode and return it
            var finalSyntaxTree = CSharpSyntaxTree.ParseText(finalContent);
            return finalSyntaxTree.GetRoot();
        }

        // TODO: format heading code to be aligned and preserve empty lines

        private static SyntaxNode NormalizeWhitespace(SyntaxNode scriptRoot)
        {
            // Convert the root to a string and split by lines
            var scriptContent = scriptRoot.ToFullString();
            var lines = scriptContent.Split(new[] { Environment.NewLine }, StringSplitOptions.None).ToList();

            // Find the line containing the marker
            int markerIndex = lines.FindIndex(line => line.Contains("!!! DONT CHANGE ANYTHING BELOW THIS LINE !!!"));

            if (markerIndex == -1)
            {
                // If the marker isn't found, return the original script
                return scriptRoot;
            }

            // Now we need to preserve the first (markerIndex + 3) lines and normalize the rest
            var untouchedLines = lines.Take(markerIndex + 3).ToList();
            var linesToNormalize = lines.Skip(markerIndex + 3).ToList();

            // Join the lines to normalize and parse them into a SyntaxTree
            var codeToNormalize = string.Join(Environment.NewLine, linesToNormalize);
            var normalizedTree = CSharpSyntaxTree.ParseText(codeToNormalize).GetRoot().NormalizeWhitespace();

            // Convert the normalized syntax tree back to a string
            var normalizedCode = normalizedTree.ToFullString();

            // Combine the untouched lines with the normalized code
            var finalContent = string.Join(Environment.NewLine, untouchedLines) + Environment.NewLine + normalizedCode;

            // Parse the final content back into a SyntaxNode and return it
            var finalSyntaxTree = CSharpSyntaxTree.ParseText(finalContent);
            return finalSyntaxTree.GetRoot();
        }

        public static SyntaxNode Minify(SyntaxNode scriptRoot)
        {
            return Minifier.Minify(scriptRoot);
        }
    }
}

