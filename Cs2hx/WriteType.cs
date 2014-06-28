using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Roslyn.Compilers.Common;
using Roslyn.Compilers.CSharp;

namespace Cs2hx
{
	static class WriteType
	{

		public static void Go()
		{
			var partials = TypeState.Instance.Partials;
			var first = partials.First();


			using (var writer = new HaxeWriter(first.Symbol.ContainingNamespace.FullName(), TypeState.Instance.TypeName))
			{
				var bases = partials
					.Select(o => o.Syntax.BaseList)
					.Where(o => o != null)
					.SelectMany(o => o.Types)
					.Select(o => (TypeSymbol)Program.GetModel(o).GetTypeInfo(o).ConvertedType)
					.Distinct()
					.ToList();

				var interfaces = bases.Where(o => o.TypeKind == TypeKind.Interface).ToList();

				TypeState.Instance.DerivesFromObject = bases.Count == interfaces.Count;

				writer.WriteLine("package " + first.Symbol.ContainingNamespace.FullName().ToLower() + @";");

				WriteImports.Go(writer);
                var isAbstract = false;
				switch (first.Syntax.Kind)
				{
					case SyntaxKind.ClassDeclaration:
					case SyntaxKind.StructDeclaration:
					case SyntaxKind.EnumDeclaration:
                        //check if class has overloaded operators
                        //if it has overloaded operators then add it to the list of abstract classes using the Utilities class
                        isAbstract = TypeProcessor.ConvertAbstractType(first.Symbol) != String.Empty;
                        writer.isAbstract = true;
                        if (isAbstract)
                        {
                            writer.Write("abstract ");
                        }
                        else
                            writer.Write("class ");

						break;
					case SyntaxKind.InterfaceDeclaration:
						writer.Write("interface ");
						break;
					default:
						throw new Exception(first.Syntax.Kind.ToString());
				}
				
				writer.Write(TypeState.Instance.TypeName);

                if (isAbstract)
                {
                    writer.Write(String.Format("(_{0}) from _{0} to _{0} ", TypeState.Instance.TypeName));
                }

				if (first.Syntax is TypeDeclarationSyntax)
				{
					//Look for generic arguments 
					var genericArgs = partials
						.Select(o => o.Syntax)
						.Cast<TypeDeclarationSyntax>()
						.Where(o => o.TypeParameterList != null)
						.SelectMany(o => o.TypeParameterList.Parameters)
						.ToList();

					if (genericArgs.Count > 0)
					{
						writer.Write("<");
						writer.Write(string.Join(", ", genericArgs.Select(o => WriteMethod.TypeParameter(o, partials.SelectMany(z => z.Syntax.As<TypeDeclarationSyntax>().ConstraintClauses)))));
						writer.Write(">");
					}

					foreach (var baseType in bases)
					{
						var baseTypeHaxe = TypeProcessor.ConvertType(baseType);

						if (baseTypeHaxe.StartsWith("Array<"))
							continue;

						writer.Write(" ");

						if (baseType.TypeKind == TypeKind.Interface)
						{
							writer.Write("implements ");
							writer.Write(baseTypeHaxe);
						}
						else
						{
							writer.Write("extends ");
							writer.Write(baseTypeHaxe);
						}
					}
				}

				writer.Write("\r\n");

				writer.WriteOpenBrace();

				if (first.Syntax is EnumDeclarationSyntax)
					WriteEnumBody.Go(writer, TypeState.Instance.Partials.Select(o => o.Syntax).Cast<EnumDeclarationSyntax>().SelectMany(o => o.Members).Where(o => !Program.DoNotWrite.ContainsKey(o)));
				else
				{
					TypeState.Instance.AllMembers = partials.Select(o => o.Syntax).Cast<TypeDeclarationSyntax>().SelectMany(o => o.Members).Where(o => !Program.DoNotWrite.ContainsKey(o)).ToList();

					foreach (var partial in partials)
					{
						foreach (var member in partial.Syntax.As<TypeDeclarationSyntax>().Members)
						{
							if (!(member is ClassDeclarationSyntax) && !(member is EnumDeclarationSyntax))
								Core.Write(writer, member);
						}
					}

					if (first.Syntax.Kind != SyntaxKind.InterfaceDeclaration)
					{
						//Normally constructors will be written as we traverse the tree.  However, if there are no constructors, we must manually write them out since there are cases where we need a constructor in haxe while C# had none.
						var ctors = TypeState.Instance.AllMembers.OfType<ConstructorDeclarationSyntax>().ToList();
						var instanceCtors = ctors.Where(o => !o.Modifiers.Any(SyntaxKind.StaticKeyword));

						if (instanceCtors.Count() > 1)
							throw new Exception("Overloaded constructors are not supported.  Consider changing all but one to static Create methods " + Utility.Descriptor(first.Syntax));

						if (ctors.None(o => o.Modifiers.Any(SyntaxKind.StaticKeyword)))
							WriteConstructor.WriteStaticConstructor(writer, null);
						if (instanceCtors.None())
							WriteConstructor.WriteInstanceConstructor(writer, null);
						
					}
				}

				writer.WriteCloseBrace();
                if (isAbstract) {
                    using (var writerType = new HaxeWriter(first.Symbol.ContainingNamespace.FullName(), "_" + TypeState.Instance.TypeName))
                    {
                        writerType.WriteLine("package " + first.Symbol.ContainingNamespace.FullName().ToLower() + @";");
                        WriteImports.Go(writerType);
                        writerType.WriteLine();
                        writerType.Write("class _");
                        writerType.Write(TypeState.Instance.TypeName);
                        writerType.WriteOpenBrace();
                        writerType.WriteLine();
                        foreach (var partial in partials)
                        {
                            foreach (var member in partial.Syntax.As<TypeDeclarationSyntax>().Members)
                            {
                                if (!(member is ClassDeclarationSyntax) && !(member is EnumDeclarationSyntax) && !(member is OperatorDeclarationSyntax))
                                    Core.Write(writerType, member);
                            }
                        }
                        var ctors = TypeState.Instance.AllMembers.OfType<ConstructorDeclarationSyntax>().ToList();
                        var instanceCtors = ctors.Where(o => !o.Modifiers.Any(SyntaxKind.StaticKeyword));

                        if (instanceCtors.Count() > 1)
                            throw new Exception("Overloaded constructors are not supported.  Consider changing all but one to static Create methods " + Utility.Descriptor(first.Syntax));

                        if (ctors.None(o => o.Modifiers.Any(SyntaxKind.StaticKeyword)))
                            WriteConstructor.WriteStaticConstructor(writerType, null);
                        if (instanceCtors.None())
                            WriteConstructor.WriteInstanceConstructor(writerType, null);
                        writerType.WriteCloseBrace();
                    }

                }
			}
		}

		public static string TypeName(NamedTypeSymbol type)
		{
			var sb = new StringBuilder(type.Name);

			while (type.ContainingType != null)
			{
				type = type.ContainingType;
				sb.Insert(0, type.Name + "_");
			}

			return sb.ToString();
		}
	}
}
