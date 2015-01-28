using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cs2hx
{

    internal static class WriteField
    {
        public static void Go(HaxeWriter writer, FieldDeclarationSyntax field)
        {
            foreach (var declaration in field.Declaration.Variables)
                Go(writer, field.Modifiers, declaration.Identifier.ValueText, field.Declaration.Type, declaration.Initializer);
        }

		public static void WriteFieldModifiers(HaxeWriter writer, SyntaxTokenList modifiers)
		{
			if (modifiers.Any(SyntaxKind.PublicKeyword) || modifiers.Any(SyntaxKind.ProtectedKeyword) || modifiers.Any(SyntaxKind.InternalKeyword))
				writer.Write("public ");
			if (modifiers.Any(SyntaxKind.PrivateKeyword))
				writer.Write("private ");
			if (modifiers.Any(SyntaxKind.StaticKeyword) || modifiers.Any(SyntaxKind.ConstKeyword))
				writer.Write("static ");
		}

        public static void Go(HaxeWriter writer, SyntaxTokenList modifiers, string name, TypeSyntax type, EqualsValueClauseSyntax initializerOpt = null)
        {
            writer.WriteIndent();

            var ContainerClassName = type.Parent.Parent.Parent.As<ClassDeclarationSyntax>().Identifier.ValueText;
            var ContainerNsName = "";
            SyntaxNode currType = type;
            while(ContainerNsName == ""){
                if (currType.Parent is NamespaceDeclarationSyntax)
                    ContainerNsName = currType.Parent.As<NamespaceDeclarationSyntax>().Name.As<IdentifierNameSyntax>().Identifier.ValueText;
                else
                    currType = currType.Parent;
            }
            var isConst = IsConst(modifiers, initializerOpt, type);
            var isAbstract = TypeProcessor.IsContainerTypeAbstract(ContainerNsName, ContainerClassName);

            if (writer.isAbstract == false)
            {
                WriteFieldModifiers(writer, modifiers);
                if (isConst)
                    writer.Write("inline ");
            }
            writer.Write("var ");

            writer.Write(name);
            if (writer.isAbstract) writer.Write("(get,set)");
			writer.Write(TypeProcessor.ConvertTypeWithColon(type));

			if (isConst)
			{
				writer.Write(" = ");
				Core.Write(writer, initializerOpt.Value);
			}

            writer.Write(";");
            writer.WriteLine();



            if (writer.isAbstract)
            {
                writer.WriteIndent();
                writer.Write(" public function get_");
                writer.Write(name);
                writer.Write("() return this.");
                writer.Write(name);
                writer.Write(";");
                writer.WriteLine();
                writer.WriteIndent();
                writer.Write("public function set_");
                writer.Write(name);
                writer.Write("(");
                writer.Write(name);
                writer.Write(")");
                writer.Write(" return this.");
                writer.Write(name);
                writer.Write(" = ");
                writer.Write(name);
                writer.Write(";");
                writer.WriteLine();
            }

        }

		public static bool IsConst(SyntaxTokenList modifiers, EqualsValueClauseSyntax initializerOpt, TypeSyntax type)
		{
			var t = TypeProcessor.ConvertType(type);

			return (modifiers.Any(SyntaxKind.ConstKeyword)
				|| (modifiers.Any(SyntaxKind.ReadOnlyKeyword) && modifiers.Any(SyntaxKind.StaticKeyword) && initializerOpt != null))
				&& (t == "Int" || t == "String" || t == "Bool" || t == "Float");
		}
    }
}
