using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Roslyn.Compilers.CSharp;

namespace Cs2hx
{
    class thisRewriter : SyntaxRewriter
    {
        private bool ScanningParams = true;
        private String name = "";
        public override SyntaxToken VisitToken(SyntaxToken token)
        {
            if (token.Kind == SyntaxKind.IdentifierToken && ((token.Parent.Kind == SyntaxKind.Parameter) || (token.Parent.Parent.Kind == SyntaxKind.ParameterList) || (token.Parent.Parent.Kind == SyntaxKind.MemberAccessExpression)))
            {
                if (ScanningParams)
                {
                    name = token.ValueText;
                    ScanningParams = false;
                }
                if(token.ValueText == name)
                    return Syntax.Identifier("this");
            }
            return token;
        }
    }

    static class WriteOperator
    {
        public static void Go(HaxeWriter writer, OperatorDeclarationSyntax method)
        {
            var op = method.OperatorToken.ValueText;
			var methodSymbol = Program.GetModel(method).GetDeclaredSymbol(method);

            writer.WriteIndent();

            var isIncrementorDecrement = op.Equals("++") || op.Equals("--") ? true : false;
            var returnType = "";
            if (isIncrementorDecrement)
            {
                writer.Write("@:op(A");

                writer.Write(op);

                writer.Write(" ) public inline function ");
            }
            else
            {
                writer.Write("@:op(A ");

                writer.Write(op);

                writer.Write(" B) static public function ");
                returnType = TypeProcessor.ConvertTypeWithColon(method.ReturnType);
            }
            switch (op)
            {
                case "+":
                    writer.Write("add");
                    break;
                case "*":
                    writer.Write("scalar");
                    break;
                case "*=":
                    writer.Write("scalarAssign");
                    break;
                case "/":
                    writer.Write("div");
                    break;
                case ">":
                    writer.Write("gt");
                    break;
                case "<":
                    writer.Write("lt");
                    break;
                case ">=":
                    writer.Write("gte");
                    break;
                case "<=":
                    writer.Write("lte");
                    break;
                case "==":
                    writer.Write("eq");
                    break;
                case "!=":
                    writer.Write("neq");
                    break;
                case "++":
                    writer.Write("inc");
                    break;
                case "--":
                    writer.Write("dec");
                    break;
                case "-":
                    writer.Write("sub");
                    break;
            }
            if (isIncrementorDecrement)
            {
                writer.Write("( )");
                var Param = method.ParameterList.Parameters.First();
                var rewriter = new thisRewriter();
                method = (OperatorDeclarationSyntax)rewriter.Visit((SyntaxNode)method);
                
               // Param.ReplaceNode(thisNode, Roslyn.Compilers.SyntaxRemoveOptions.KeepNoTrivia);
            }
            else
                writer.Write("( " + method.ParameterList.Parameters.First().Identifier.ValueText + returnType + " , " + method.ParameterList.Parameters.Last().Identifier.ValueText + returnType + " )" + returnType);

            writer.WriteLine();
            writer.WriteOpenBrace();

			if (method.Body != null)
			{
                foreach (var statement in method.Body.Statements)
                {
                    if(isIncrementorDecrement ){ 
                        if(!statement.GetText().ToString().Contains("return"))
                        writer.Write(statement.GetText().ToString());
                    } else
                        Core.Write(writer, statement);
                }

				TriviaProcessor.ProcessTrivias(writer, method.Body.DescendantTrivia());
			}

            writer.WriteCloseBrace();

        }

    }
} 
