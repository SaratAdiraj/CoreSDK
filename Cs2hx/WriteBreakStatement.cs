﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cs2hx
{
	static class WriteBreakStatement
	{
		public static void Go(HaxeWriter writer, BreakStatementSyntax statement)
		{
			writer.WriteLine("break;");
		}
	}
}
