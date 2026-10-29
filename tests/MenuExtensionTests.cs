using System;
using System.IO;
using System.Text;
static class MenuTest {
 static int Main(string[] args){
  string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
  try{
   Engine.Store=Path.Combine(root,"backups");
   string module=Path.Combine(root,"Qt5Compat","GraphicalEffects");Directory.CreateDirectory(module);
   File.WriteAllText(Path.Combine(module,"qmldir"),"module Qt5Compat.GraphicalEffects\nplugin qtgraphicaleffectsplugin\nprefer :/qt-project.org/imports/Qt5Compat/GraphicalEffects/\nDropShadow 6.0 DropShadow.qml\nRectangularGlow 6.0 RectangularGlow.qml\n");
   string previous=File.ReadAllText(args[1]);
   foreach(string n in new[]{"DropShadow.qml","RectangularGlow.qml"})File.WriteAllText(Path.Combine(module,n),"import QtQuick\nItem {\nid: rootItem\n"+previous+"\n}");
   File.WriteAllText(Path.Combine(module,"BlendPlusMenuLimiter.qml"),"// __BlendPlusMenuLimiter old helper");
   string exe=Path.Combine(root,"JianyingPro.exe");
   MenuExtension.Install(exe);MenuExtension.Install(exe);
   if(File.ReadAllText(Path.Combine(module,"qmldir")).Contains("prefer "))throw new Exception("Embedded resource still preferred");
   foreach(string n in new[]{"DropShadow.qml","RectangularGlow.qml"}){
    string text=File.ReadAllText(Path.Combine(module,n));
    if(text.Split(new[]{"// BlendPlus menu hook v2"},StringSplitOptions.None).Length!=2)throw new Exception("Repeated hook");
    if(text.Contains("unsigned QML extension"))throw new Exception("Old hook remains");
   }
   MenuExtension.Restore(exe);
   if(!File.ReadAllText(Path.Combine(module,"qmldir")).Contains("prefer "))throw new Exception("Original preference not restored");
   if(File.Exists(Path.Combine(module,"BlendPlusMenuLimiter.qml")))throw new Exception("Helper remains");
   foreach(string n in new[]{"DropShadow.qml","RectangularGlow.qml"})if(File.ReadAllText(Path.Combine(module,n)).Contains("BlendPlus"))throw new Exception("Restored file retains hook");
   Console.WriteLine("PASS: resource preference; legacy migration; repeat installation; clean restoration.");return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}
 }
}

