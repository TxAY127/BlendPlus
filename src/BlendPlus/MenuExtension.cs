using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Reflection;

// Load the editable Qt module instead of its embedded copies. Vendor DLLs stay intact.
static class MenuExtension {
 static readonly string[] Names={"qmldir","DropShadow.qml","RectangularGlow.qml","BlendPlusMenuLimiter.qml"};
 static string Asset(string name){using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream(name))using(var r=new StreamReader(s,Encoding.UTF8))return r.ReadToEnd();}
 static string Module(string exe){return Path.Combine(Path.GetDirectoryName(exe),"Qt5Compat","GraphicalEffects");}
 static string Backup(string exe){return Path.Combine(Engine.Backup(Engine.ResourceRoot(exe)),"qml-menu-v2");}
 public static void Validate(string exe){
  string dir=Module(exe);
  foreach(string name in Names.Take(3))if(!File.Exists(Path.Combine(dir,name)))throw new Exception("这个版本的菜单暂未适配。");
  foreach(string name in Names.Skip(1).Take(2))if(!File.ReadAllText(Path.Combine(dir,name)).Contains("id: rootItem"))throw new Exception("菜单组件结构不同，已停止安装。");
  string config=File.ReadAllText(Path.Combine(dir,"qmldir"));
  if(!config.Contains("module Qt5Compat.GraphicalEffects"))throw new Exception("菜单模块结构不同，已停止安装。");
 }
 static void Write(string path,byte[] bytes){
  string temp=path+".blendplus-"+Guid.NewGuid().ToString("N")+".tmp";
  try{File.WriteAllBytes(temp,bytes);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}
 }
 static byte[] Bytes(string value){return new UTF8Encoding(false).GetBytes(value);}
 static string Clean(string original){
  string previous=Asset("previous-menu-hook.txt").Replace("\r\n","\n");
  string text=original.Replace("\r\n","\n");
  if(text.Contains("// BlendPlus menu limiter: unsigned QML extension, no signed binary changes.")){
   if(!text.Contains(previous))throw new Exception("旧菜单补丁无法识别，已停止安装。");
   text=text.Replace(previous,"");
  }
  return text;
 }
 public static void Install(string exe){
  Validate(exe);string dir=Module(exe),backup=Backup(exe);
  var prior=new Dictionary<string,byte[]>();foreach(string n in Names){string p=Path.Combine(dir,n);prior[n]=File.Exists(p)?File.ReadAllBytes(p):null;}
  Directory.CreateDirectory(backup);
  bool ready=File.Exists(Path.Combine(backup,"ready"));
  try{
   if(!ready){
    foreach(string n in Names.Take(3)){
     string text=File.ReadAllText(Path.Combine(dir,n));
     if(n.EndsWith(".qml"))text=Clean(text);
     File.WriteAllBytes(Path.Combine(backup,n),Bytes(text));
    }
    // The previous helper belongs to BlendPlus too; restoring removes it.
    string helper=Path.Combine(dir,Names[3]);
    if(File.Exists(helper)&&!File.ReadAllText(helper).Contains("__BlendPlusMenuLimiter"))throw new Exception("菜单扩展文件已被占用。");
    File.WriteAllText(Path.Combine(backup,"ready"),"BlendPlus menu v2");
   }
   string config=File.ReadAllText(Path.Combine(backup,"qmldir"));
   config=String.Join("\n",config.Replace("\r\n","\n").Split('\n').Where(line=>!line.TrimStart().StartsWith("prefer ",StringComparison.Ordinal)));
   foreach(string n in Names.Skip(1).Take(2)){
    string text=File.ReadAllText(Path.Combine(backup,n));
    if(!text.Contains("import QtQuick.Window"))text=text.Replace("import QtQuick\n","import QtQuick\nimport QtQuick.Window\n");
    string hook=Asset("menu-hook.txt");
    int at=text.IndexOf("id: rootItem",StringComparison.Ordinal)+"id: rootItem".Length;
    text=text.Insert(at,"\n"+hook+"\n");Write(Path.Combine(dir,n),Bytes(text));
   }
   Write(Path.Combine(dir,Names[3]),Bytes(Asset("BlendPlusMenuLimiter.qml")));
   // Activate only after every referenced QML file is present.
   Write(Path.Combine(dir,"qmldir"),Bytes(config));
  }catch{
   foreach(var p in prior){try{string file=Path.Combine(dir,p.Key);if(p.Value==null){if(File.Exists(file))File.Delete(file);}else Write(file,p.Value);}catch{}}
   if(!ready){foreach(string n in Names.Take(3)){string p=Path.Combine(backup,n);if(File.Exists(p))File.Delete(p);}string marker=Path.Combine(backup,"ready");if(File.Exists(marker))File.Delete(marker);}
   throw;
  }
 }
 public static void Restore(string exe){
  string dir=Module(exe),backup=Backup(exe);if(!File.Exists(Path.Combine(backup,"ready")))return;
  // Check all backups before writing anything.
  foreach(string n in Names.Take(3))if(!File.Exists(Path.Combine(backup,n)))throw new Exception("菜单备份不完整。");
  foreach(string n in Names.Take(3))Write(Path.Combine(dir,n),File.ReadAllBytes(Path.Combine(backup,n)));
  string helper=Path.Combine(dir,Names[3]);if(File.Exists(helper)&&File.ReadAllText(helper).Contains("__BlendPlusMenuLimiter"))File.Delete(helper);
 }
}
