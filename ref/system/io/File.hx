package system.io;
import haxe.ds.StringMap.StringMap;
import haxe.io.Bytes;
import system.NotImplementedException;

class File
{

	public function new() 
	{
		
	}
	
	public static function Exists(path:String):Bool
	{
		#if sys
			return sys.FileSystem.exists(path);
		#else
			return throw new NotImplementedException();
		#end		
	}
	public static function Delete(path:String):Void
	{
		#if sys
			sys.FileSystem.deleteFile(path);
		#else
			return throw new NotImplementedException();
		#end		
	}
	
	public static function AppendAllText(path:String, contents:String):Void
	{
		#if sys
			sys.io.File.append(path,false).writeString(contents);
		#else
			return throw new NotImplementedException();
		#end		
	}
	public static function OpenRead(path:String):FileStream
	{
		#if sys
		    return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
		#else
			return throw new NotImplementedException();
		#end		
	}
	public static function ReadAllBytes(path:String):Bytes
	{
		return throw new NotImplementedException();
	}
	public static function ReadAllText(path:String):String
	{
		return throw new NotImplementedException();
	}
	public static function WriteAllBytes(path:String, bytes:Bytes):Void
	{
		throw new NotImplementedException();
	}
	public static function WriteAllText(path:String, text:String):Void
	{
		throw new NotImplementedException();
	}
	
	public static function GetAttributes(path:String):Int
	{
		return throw new NotImplementedException();
	}
	public static function SetAttributes(path:String, attrs:Int):Void
	{
		throw new NotImplementedException();
	}

	public static function Copy(src:String, dest:String):Void
	{
		throw new NotImplementedException();
	}
	public static function Copy_String_String_Boolean(src:String, dest:String, overwrite:Bool):Void
	{
		throw new NotImplementedException();
	}
	public static function Move(src:String, dest:String):Void
	{
		throw new NotImplementedException();
	}
}