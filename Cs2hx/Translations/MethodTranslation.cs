﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cs2hx.Translations
{
    class MethodTranslation
    {
		public static MethodTranslation Get(IMethodSymbol origMethodSymbol)
		{
			var methodSymbol = origMethodSymbol.OriginalDefinition.As<IMethodSymbol>().UnReduce();

			var sourceName = methodSymbol.ContainingNamespace.FullNameWithDot() + methodSymbol.ContainingType.Name;
			var arguments = string.Join(" ", methodSymbol.Parameters.ToList().Select(o => o.Type.ToString()));

			var matches = TranslationManager.Methods.Where(o => o.Match == methodSymbol.Name)
				.Where(o => o.SourceObject == sourceName || o.SourceObject == null || o.SourceObject == "*")
				.Where(o => o.ArgumentTypes == null || o.ArgumentTypes == arguments)
				.Where(o => o.TypeParametersMatch(origMethodSymbol))
				.ToList();

			if (matches.Count > 1)
			{
				var matches2 = matches.Where(o => o.ArgumentTypes == arguments).ToList();

				if (matches2.Count > 0)
					matches = matches2;
				else
					matches = matches.Where(o => o.ArgumentTypes == null).ToList();

				if (matches.Count > 1)
					matches = matches.Except(matches.Where(o => o.SourceObject == "*")).ToList();
			}

			if (matches.Count == 0)
				return null;

			return matches.SingleOrDefault();
		}

		private bool TypeParametersMatch(IMethodSymbol methodSymbol)
		{
			foreach (var match in this.MatchTypeParameters)
				if (methodSymbol.TypeArguments[match.TypeParameterIndex].ToString() != match.Match)
					return false;

			return true;

		}

		public string SourceObject { get; set; }
		public string Match { get; set; }
        public string ReplaceWith { get; set; }
        private List<ArgumentModifier> Arguments { get; set; }
		private List<MatchTypeParameter> MatchTypeParameters { get; set; }
        public string ExtensionNamespace { get; set; }
        public bool SkipExtensionParameter { get; set; }
		public string ArgumentTypes { get; set; }

		public bool HasComplexReplaceWith
		{
			get { return DoComplexReplaceWith != null; }
		}
		public Action<HaxeWriter, MemberAccessExpressionSyntax> DoComplexReplaceWith;

        public bool IsExtensionMethod
        {
            get
            {
                return this.ExtensionNamespace != null;
            }
        }

        public MethodTranslation(XElement data)
        {
			TranslationManager.InitProperties(this, data);

            Arguments = data.Elements("Argument").Select(o => TranslationManager.InitProperties(new ArgumentModifier(), o)).ToList();
			MatchTypeParameters = data.Elements("MatchTypeParameter").Select(o => TranslationManager.InitProperties(new MatchTypeParameter(), o)).ToList();

			if (data.Element("ReplaceWith") != null)
			{
				DoComplexReplaceWith = (writer, expression) =>
					{
						foreach (var element in data.Element("ReplaceWith").Elements())
						{
							switch (element.Name.LocalName)
							{
								case "String":
									writer.Write(ReplaceSpecialIndicators(element.Value, expression));
									break;
								case "Expression":
									Core.Write(writer, expression.Expression);
									break;
								default:
									throw new Exception("Unexpected element name " + element.Name);
							}
						}
					};
			}
        }

		private string ReplaceSpecialIndicators(string rawString, ExpressionSyntax expression)
		{
			if (rawString.Contains("{genericType}"))
				rawString = ReplaceGenericVar(rawString, expression);

			return rawString;
		}

		private string ReplaceGenericVar(string rawString, ExpressionSyntax expression)
		{
			var name = expression.As<MemberAccessExpressionSyntax>().Name.As<GenericNameSyntax>();

			var genericVar = TypeProcessor.ConvertType(name.TypeArgumentList.Arguments.Single());

			return rawString.Replace("{genericType}", genericVar);
		}


		internal IEnumerable<TransformedArgument> TranslateParameters(IEnumerable<ArgumentSyntax> args, ExpressionSyntax expression)
		{
			//Copy it
			var list = args.Select(o => new TransformedArgument(o)).ToList();

			foreach (var arg in Arguments)
			{
				if (arg.Action == "Delete")
					list.RemoveAt(arg.Location);
				else if (arg.Action == "DeleteIfPresent")
				{
					if (list.Count > arg.Location)
						list.RemoveAt(arg.Location);
				}
				else if (arg.Action.StartsWith("MoveTo "))
				{
					var item = list[arg.Location];
					list.RemoveAt(arg.Location);
					list.Insert(int.Parse(arg.Action.Substring(7)), item);
				}
				else if (arg.Action.StartsWith("Insert "))
					list.Insert(arg.Location, new TransformedArgument(ReplaceSpecialIndicators(arg.Action.Substring(7), expression)));
				else
					throw new Exception("Need handler for " + arg.Action);
			}

			return list;
		}


		class ArgumentModifier
		{
			public int Location { get; set; }
			public string Action { get; set; }
		}

		class MatchTypeParameter
		{
			public int TypeParameterIndex { get; set; }
			public string Match { get; set; }
		}
    }

}
