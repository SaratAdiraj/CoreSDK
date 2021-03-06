﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cs2hx.Translations;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;


namespace Cs2hx
{
	static class WriteObjectCreationExpression
	{
		public static void Go(HaxeWriter writer, ObjectCreationExpressionSyntax expression)
		{
			if (expression.ArgumentList == null)
				throw new Exception("Types must be initialized with parenthesis. Object initialization syntax is not supported. " + Utility.Descriptor(expression));

			var model = Program.GetModel(expression);
			var type = model.GetTypeInfo(expression).Type;

			if (type.SpecialType == SpecialType.System_Object)
			{
				//new object() results in the CsObject type being made.  This is only really useful for locking
				writer.Write("new CsObject()");
			}
			else
			{
				var methodSymbol = model.GetSymbolInfo(expression).Symbol.As<IMethodSymbol>();

				var translateOpt = MethodTranslation.Get(methodSymbol);
				

				writer.Write("new ");
                //check if the expression is declared inside an abstract class type
                //if it belongs to an abstract class and 
                //if expression.Type is the name of the abstract class, then add an underscore prefix
                var ContainerClassName = TypeProcessor.ConvertType(expression.Type).ToLower().Split('.')[1];
                var ContainerNsName = TypeProcessor.ConvertType(expression.Type).ToLower().Split('.')[0];


                if (TypeProcessor.IsContainerTypeAbstract(ContainerNsName, ContainerClassName))
                {
                        writer.Write(TypeProcessor.ConvertType(expression.Type).Replace(".","._"));
                }
                else
                writer.Write(TypeProcessor.ConvertType(expression.Type));
				writer.Write("(");

				bool first = true;
				foreach (var param in TranslateParameters(translateOpt, WriteInvocationExpression.SortArguments(methodSymbol, expression.ArgumentList.Arguments, expression), expression))
				{
					if (first)
						first = false;
					else
						writer.Write(", ");

					param.Write(writer);
				}

				writer.Write(")");
			}
		}

		private static IEnumerable<TransformedArgument> TranslateParameters(MethodTranslation translateOpt, IEnumerable<ArgumentSyntax> list, ObjectCreationExpressionSyntax invoke)
		{
			if (translateOpt == null)
				return list.Select(o => new TransformedArgument(o));
			else if (translateOpt is MethodTranslation)
				return translateOpt.As<MethodTranslation>().TranslateParameters(list, invoke);
			else
				throw new Exception("Need handler for " + translateOpt.GetType().Name);
		}

	}
}
