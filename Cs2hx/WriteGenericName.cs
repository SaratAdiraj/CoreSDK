﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Roslyn.Compilers.CSharp;

namespace Cs2hx
{
	static class WriteGenericName
	{
		public static void Go(HaxeWriter writer, GenericNameSyntax name)
		{
			writer.Write(name.Identifier.ValueText); //leave off generic parameters for haxe
		}
	}
}
