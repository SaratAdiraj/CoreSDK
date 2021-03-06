﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Cs2hx.Translations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cs2hx
{
    static class TypeProcessor
    {
		public static string DefaultValue(string haxeType)
		{
			switch (haxeType)
			{
				case "Int":
				case "Float":
					return "0";
				case "Bool":
					return "false";
				default:
					return "null";
			}
		}

		public static string TryConvertType(SyntaxNode node)
		{
			if (node == null)
				return null;

			var attrs = Utility.GetCS2HXAttribute(node);
			if (attrs.ContainsKey("ReplaceWithType"))
				return attrs["ReplaceWithType"];

			var model = Program.GetModel(node).As<SemanticModel>();
			var typeInfo = model.GetTypeInfo(node);

			if (typeInfo.ConvertedType is IErrorTypeSymbol)
				typeInfo = model.GetTypeInfo(node.Parent); //not sure why Roslyn can't find the type of some type nodes, but telling it to use the parent's seems to work

			var t = typeInfo.ConvertedType;
			
			if (t == null || t is IErrorTypeSymbol)
				return null;

			return ConvertType((ITypeSymbol)t);
		}

		public static string ConvertTypeWithColon(SyntaxNode node)
		{
			var ret = TryConvertType(node);

			if (ret == null)
				return "";
			else
				return ":" + ret;
		}

        
        public static string ConvertType(SyntaxNode node)
        {
			var ret = TryConvertType(node);

			if (ret == null)
				throw new Exception("Type could not be determined for " + node);

			return ret;
		}

		public static string ConvertTypeWithColon(ITypeSymbol node)
		{
			var ret = ConvertType(node);

			if (ret == null)
				return "";
			else
				return ":" + ret;
		}

        private static ConcurrentDictionary<ITypeSymbol, string> _abstractTypes = new ConcurrentDictionary<ITypeSymbol, string>();
        public static string ConvertAbstractType(ITypeSymbol typeInfo)
        {
            string cachedValue = String.Empty;
            var operatorOverloads = typeInfo.GetMembers().Where(o => o.DeclaringSyntaxReferences.FirstOrDefault() is OperatorDeclarationSyntax).ToList();
            if (operatorOverloads.Count > 0)
            {
                if (_abstractTypes.TryGetValue(typeInfo, out cachedValue))
                    return cachedValue;
                cachedValue = ConvertTypeUncached(typeInfo);
                _abstractTypes.TryAdd(typeInfo, cachedValue.ToLower());
            }
            return cachedValue;
        }

        public static Boolean IsContainerTypeAbstract(string ns, string cls) {
            return _abstractTypes.Values.Contains((ns + "." + cls).ToLower());
        }
        

		private static ConcurrentDictionary<ITypeSymbol, string> _cachedTypes = new ConcurrentDictionary<ITypeSymbol, string>();

		public static string ConvertType(ITypeSymbol typeInfo)
		{
			string cachedValue;
			if (_cachedTypes.TryGetValue(typeInfo, out cachedValue))
				return cachedValue;

			cachedValue = ConvertTypeUncached(typeInfo);
			_cachedTypes.TryAdd(typeInfo, cachedValue);
			return cachedValue;
		}

		private static string ConvertTypeUncached(ITypeSymbol typeSymbol)
		{
			if (typeSymbol.IsAnonymousType)
				return WriteAnonymousObjectCreationExpression.TypeName(typeSymbol.As<INamedTypeSymbol>());

			var array = typeSymbol as IArrayTypeSymbol;

			if (array != null)
			{
				if (array.ElementType.ToString() == "byte")
					return "haxe.io.Bytes"; //byte arrays become haxe.io.Bytes
				else
					return "Array<" + (ConvertType(array.ElementType) ?? "Dynamic") + ">";
			}

			var typeInfoStr = typeSymbol.ToString();

			var named = typeSymbol as INamedTypeSymbol;

			if (typeSymbol.TypeKind == TypeKind.TypeParameter)
				return typeSymbol.Name;

			if (typeSymbol.TypeKind == TypeKind.Delegate)
			{
				var dlg = named.DelegateInvokeMethod.As<IMethodSymbol>();
				if (dlg.Parameters.Count() == 0)
					return "(Void -> " + ConvertType(dlg.ReturnType) + ")";
				else
					return "(" + string.Join("", dlg.Parameters.ToList().Select(o => ConvertType(o.Type) + " -> ")) + ConvertType(dlg.ReturnType) + ")";
			}

			if (typeSymbol.TypeKind == TypeKind.Enum)
				return "Int"; //enums are always ints

			if (named != null && named.Name == "Nullable" && named.ContainingNamespace.ToString() == "System")
			{
				//Nullable types get replaced by our Nullable_ alternatives
				var nullableType = ConvertType(named.TypeArguments.Single());
				if (nullableType == "Int" || nullableType == "Bool" || nullableType == "Float")
					return "Nullable_" + nullableType;
				else
					return "Nullable<" + nullableType + ">";
			}

			var typeStr = GenericTypeName(typeSymbol);

			var trans = TypeTranslation.Get(typeStr);

			if (named != null && named.IsGenericType && !named.IsUnboundGenericType && TypeArguments(named).Any() && (trans == null || trans.SkipGenericTypes == false))
				return ConvertType(named.ConstructUnboundGenericType()) + "<" + string.Join(", ", TypeArguments(named).Select(o => ConvertType(o) ?? "Dynamic")) + ">";


			switch (typeStr)
			{
				case "System.Void":
					return "Void";

				case "System.Boolean":
					return "Bool";

				case "System.Object":
					return "Dynamic";

				case "System.Int64":
				case "System.UInt64":
				case "System.Single":
				case "System.Double":
					return "Float";

				case "System.String":
					return "String";

				case "System.Int32":
				case "System.UInt32":
				case "System.Byte":
				case "System.Int16":
				case "System.UInt16":
				case "System.Char":
					return "Int";

					
				case "System.Collections.Generic.List<>":
				case "System.Collections.Generic.IList<>":
				case "System.Collections.Generic.Queue<>":
				case "System.Collections.Generic.Stack<>":
				case "System.Collections.Generic.IEnumerable<>":
				case "System.Collections.Generic.Dictionary<,>.ValueCollection":
				case "System.Collections.Generic.Dictionary<,>.KeyCollection":
				case "System.Collections.Generic.ICollection<>":
				case "System.Linq.IOrderedEnumerable<>":
				case "System.Collections.IEnumerable":
				case "System.Collections.Specialized.NameObjectCollectionBase.KeysCollection":
					return "Array";

				case "System.Array":
					return null; //in haxe, unlike C#, array must always have type arguments.  To work around this, just avoid printing the type anytime we see a bare Array type in C#. haxe will infer it.

				case "System.Collections.Generic.LinkedList<>":
					return "List";

				default:
					

					if (trans != null)
						return trans.As<Translations.TypeTranslation>().Replace(named);

					if (named != null)
						return typeSymbol.ContainingNamespace.FullNameWithDot().ToLower() + WriteType.TypeName(named);

					//This type does not get translated and gets used as-is
					return typeSymbol.ContainingNamespace.FullNameWithDot().ToLower() + typeSymbol.Name;
				
			}

        }

		private static IEnumerable<ITypeSymbol> TypeArguments(INamedTypeSymbol named)
		{
			if (named.ContainingType != null)
			{
				//Hard-code generic types for dictionaries, since I can't find a way to determine them programatically
				switch (named.Name)
				{
					case "ValueCollection":
						return new[] { named.ContainingType.TypeArguments.ElementAt(1) };
					case "KeyCollection":
						return new[] { named.ContainingType.TypeArguments.ElementAt(0) };
					default:
						return named.TypeArguments.ToList();
				}
			}

			return named.TypeArguments.ToList();
		}


        public static string HaxeLiteral(this SyntaxNode literal)
        {
			if (literal is ArgumentSyntax)
				return HaxeLiteral(literal.As<ArgumentSyntax>().Expression);
			else if (literal is LiteralExpressionSyntax)
				return literal.ToString();
			else
				throw new Exception("Need handler for " + literal.GetType().Name);
        }


		public static string GenericTypeName(ITypeSymbol typeSymbol)
		{
			if (typeSymbol == null)
				return null;

			var named = typeSymbol as INamedTypeSymbol;
			var array = typeSymbol as IArrayTypeSymbol;

			if (array != null)
				return GenericTypeName(array.ElementType) + "[]";
			else if (named != null && named.IsGenericType && !named.IsUnboundGenericType)
				return GenericTypeName(named.ConstructUnboundGenericType());
			else if (named != null && named.SpecialType != SpecialType.None)
				return named.ContainingNamespace + "." + named.Name; //this forces C# shortcuts like "int" to never be used, and instead returns System.Int32 etc.
			else
				return typeSymbol.ToString();
		}


		public static string RemoveGenericArguments(string haxeType)
		{
			var i = haxeType.IndexOf('<');
			if (i == -1)
				return haxeType;
			else
				return haxeType.Substring(0, i);
		}

		/// <summary>
		/// Returns true if this is a value type in C# but a reference type in haxe.  We try to avoid this where possible, but haxe doesn't let us define our own value types.
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public static bool ValueToReference(TypeSyntax type)
		{
			var typeSymbol = Program.GetModel(type).GetTypeInfo(type);
			if (typeSymbol.Type.IsValueType == false)
				return false;

			switch (ConvertType(typeSymbol.Type))
			{
				case "Int":
				case "Float":
				case "Bool":
				case "String":
					return false;
				default:
					return true;
			}
		}
	}
}
