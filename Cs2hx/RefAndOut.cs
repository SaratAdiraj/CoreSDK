using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cs2hx
{
	static class RefAndOut
	{
		public static void ScopeBeginning(HaxeWriter writer, Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionSyntax scope)
		{
			foreach (var invoke in scope.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>())
			{

			}
		}
	}
}
