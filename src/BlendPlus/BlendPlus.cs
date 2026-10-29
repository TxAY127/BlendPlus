using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Security.Cryptography;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using Microsoft.Win32;

class Mode { public string Key,Name,Expr; public Mode(string k,string n,string e){Key=k;Name=n;Expr=e;} }
class InstallInfo {
 public string Exe;
 public string Version {get {try{string raw=FileVersionInfo.GetVersionInfo(Exe).ProductVersion;var match=System.Text.RegularExpressions.Regex.Match(raw??"",@"\d+\.\d+\.\d+");return match.Success?match.Value:Path.GetFileName(Path.GetDirectoryName(Exe));}catch{return "未知版本";}}}
 public override string ToString(){return Version+"  ·  "+Path.GetDirectoryName(Exe);}
}
static class Engine {
 public const string Prefix="blendplus_v1_";
 public static JavaScriptSerializer Json=new JavaScriptSerializer();
 public static string Store=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BlendPlus","Backups");

 public static string SettingsPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BlendPlus","installations.json");
 static Dictionary<string,object> Settings(){try{return Read(SettingsPath);}catch{return new Dictionary<string,object>();}}
 public static string Selected(){var s=Settings();return s.ContainsKey("selected")?Convert.ToString(s["selected"]):null;}
 public static List<string> Remembered(){var s=Settings();if(!s.ContainsKey("paths"))return new List<string>();try{return ((IEnumerable)s["paths"]).Cast<object>().Select(Convert.ToString).Where(x=>!String.IsNullOrWhiteSpace(x)).ToList();}catch{return new List<string>();}}
 public static bool Candidate(string exe){try{return File.Exists(exe)&&File.Exists(Path.Combine(Path.GetDirectoryName(exe),"Resources","MixMode","MixMode.json"));}catch{return false;}}
 public static void Remember(string exe,bool selected){
 if(!Candidate(exe))return;
 var paths=Remembered();string full=Path.GetFullPath(exe);if(!paths.Contains(full,StringComparer.OrdinalIgnoreCase))paths.Add(full);
 var s=Settings();s["paths"]=paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();if(selected)s["selected"]=full;
 string text=Json.Serialize(s);if(File.Exists(SettingsPath)&&File.ReadAllText(SettingsPath)==text)return;
 Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));Atomic(SettingsPath,Utf(text));
 }
 public static InstallInfo Preferred(List<InstallInfo> items,string previous){
 string selected=String.IsNullOrEmpty(previous)?Selected():previous;
 return items.FirstOrDefault(x=>String.Equals(x.Exe,selected,StringComparison.OrdinalIgnoreCase))??items.FirstOrDefault();
 }

 public static Mode[] Modes={
 new Mode("difference","差值","abs(b-f)"), new Mode("exclusion","排除","b+f-2.0*b*f"),
 new Mode("add","线性减淡（添加）","min(b+f,vec3(1.0))"), new Mode("subtract","相减","max(b-f,vec3(0.0))"),
 new Mode("divide","相除","vec3(dv(b.r,f.r),dv(b.g,f.g),dv(b.b,f.b))"),
 new Mode("linear_light","线性光","clamp(b+2.0*f-1.0,0.0,1.0)"),
 new Mode("vivid_light","亮光","vec3(vivid(b.r,f.r),vivid(b.g,f.g),vivid(b.b,f.b))"),
 new Mode("pin_light","点光","vec3(pin(b.r,f.r),pin(b.g,f.g),pin(b.b,f.b))"),
 new Mode("hard_mix","纯色混合","step(vec3(0.5),vec3(vivid(b.r,f.r),vivid(b.g,f.g),vivid(b.b,f.b)))"),
 new Mode("darker_color","较深的颜色","lum(f)<lum(b)?f:b"), new Mode("lighter_color","较浅的颜色","lum(f)>lum(b)?f:b"),
 new Mode("hue","色相","setLum(setSat(f,sat(b)),lum(b))"),new Mode("saturation","饱和度","setLum(setSat(b,sat(f)),lum(b))"),
 new Mode("color","颜色","setLum(f,lum(b))"),new Mode("luminosity","发光度","setLum(b,lum(f))") };
 static string Hash(string v){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(v.ToLowerInvariant()))).Replace("-","").Substring(0,24);}
 public static string Backup(string root){return Path.Combine(Store,Hash(Path.GetFullPath(root)));}
 public static Dictionary<string,object> Read(string p){return Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(p));}
 public static List<Dictionary<string,object>> Entries(Dictionary<string,object> d){return ((IEnumerable)d["resourceList"]).Cast<object>().Select(x=>(Dictionary<string,object>)x).ToList();}
 public static bool Owned(Dictionary<string,object> d){return d.ContainsKey("path")&&Convert.ToString(d["path"]).StartsWith(Prefix,StringComparison.Ordinal);}
 public static string ResourceRoot(string exe){return Path.Combine(Path.GetDirectoryName(exe),"Resources","MixMode");}
 public static string Validate(string root){
 var p=Path.Combine(root,"MixMode.json");if(!File.Exists(p))throw new Exception("找不到混合模式资源。这个版本尚不支持安装。");
 var entries=Entries(Read(p));var mul=entries.FirstOrDefault(x=>Convert.ToString(x["nameId"])=="multiply_blend_mode");
 if(mul==null)throw new Exception("此版本的资源结构不同，已停止安装。");
 string rel=Convert.ToString(mul["path"]);if(Path.GetFileName(rel)!=rel||rel=="."||rel=="..")throw new Exception("资源路径不符合要求。");
 string template=Path.Combine(root,rel);
 foreach(var f in new[]{"config.json","content.json","anim.prefab","blend.material","xshader/blend.xshader"})if(!File.Exists(Path.Combine(template,f)))throw new Exception("此版本的混合模式资源不完整。");
 var shaders=Directory.GetFiles(template,"*.frag",SearchOption.AllDirectories).Where(x=>x.IndexOf("shaderGLES",StringComparison.OrdinalIgnoreCase)>=0).ToArray();
 if(shaders.Length!=1)throw new Exception("此版本的着色器结构未适配。");
 var shader=File.ReadAllText(shaders[0]);foreach(var token in new[]{"MultiplyBlendStraight(","MultiplyBlendPreMul(","baseTex","_MainTex","blendAlphaState","fgAlphaState","bgAlphaState","uv0","uv1"})if(!shader.Contains(token))throw new Exception("此版本的渲染接口未适配。");
 return template;
 }

 public static void RemoveProbe(List<string> roots){foreach(string root in roots){string list=Path.Combine(root,"MixMode.json");if(!File.Exists(list))continue;var d=Read(list);var entries=Entries(d);if(!entries.Any(x=>Convert.ToString(x["path"])=="codex_blend_difference_probe"))continue;d["resourceList"]=entries.Where(x=>Convert.ToString(x["path"])!="codex_blend_difference_probe").ToArray();Atomic(list,Utf(Json.Serialize(d)));string folder=Path.GetFullPath(Path.Combine(root,"codex_blend_difference_probe"));if(Path.GetDirectoryName(folder)!=Path.GetFullPath(root))throw new Exception("测试清理路径校验失败。");if(Directory.Exists(folder)&&(File.GetAttributes(folder)&FileAttributes.ReparsePoint)==0)Directory.Delete(folder,true);}}

 public static List<string> Roots(string exe){
 var roots=new List<string>{ResourceRoot(exe)};
 string shared=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"JianyingPro","User Data","Resources","MixMode");
 if(File.Exists(Path.Combine(shared,"MixMode.json"))&&!roots.Contains(shared,StringComparer.OrdinalIgnoreCase))roots.Add(shared);
 return roots;
 }
 public static void Guard(){if(Process.GetProcessesByName("JianyingPro").Length>0)throw new Exception("请保存工程并完全退出剪映，再点击安装或恢复。工具不会强制关闭剪映。");}
 static void CopyTree(string a,string b){
 if((File.GetAttributes(a)&FileAttributes.ReparsePoint)!=0)throw new Exception("不支持链接形式的资源目录。");
 Directory.CreateDirectory(b);foreach(var f in Directory.GetFiles(a))File.Copy(f,Path.Combine(b,Path.GetFileName(f)),true);
 foreach(var d in Directory.GetDirectories(a))CopyTree(d,Path.Combine(b,Path.GetFileName(d)));
 }
 static void Atomic(string p,byte[] bytes){var tmp=p+".blendplus-"+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllBytes(tmp,bytes);if(File.Exists(p))File.Replace(tmp,p,null);else File.Move(tmp,p);}finally{if(File.Exists(tmp))File.Delete(tmp);}}
 static byte[] Utf(string s){return new UTF8Encoding(false).GetBytes(s);}
 static void DeleteOwned(string p,string root){
 var full=Path.GetFullPath(p);var parent=Path.GetDirectoryName(full);if(!String.Equals(parent,Path.GetFullPath(root),StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(full).StartsWith(Prefix)||!File.Exists(Path.Combine(full,".blendplus-owned")))throw new Exception("恢复路径校验失败。");
 if((File.GetAttributes(full)&FileAttributes.ReparsePoint)!=0)throw new Exception("拒绝处理链接目录。");Directory.Delete(full,true);
 }
 public static void Install(string exe){
 Guard();MenuExtension.Validate(exe);RemoveProbe(Roots(exe));InstallRoots(Roots(exe));MenuExtension.Install(exe);}
 public static void InstallRoots(List<string> roots){foreach(var r in roots)Validate(r);
 var snapshots=new Dictionary<string,byte[]>();var made=new List<KeyValuePair<string,string>>();
 try{foreach(var root in roots){
 string list=Path.Combine(root,"MixMode.json");snapshots[list]=File.ReadAllBytes(list);var d=Read(list);var entries=Entries(d);string template=Validate(root);
 // Back up every resource list before touching it. Backups stay with this Windows user.
 string backup=Backup(root);Directory.CreateDirectory(backup);if(!File.Exists(Path.Combine(backup,"original.json")))File.WriteAllBytes(Path.Combine(backup,"original.json"),snapshots[list]);File.WriteAllText(Path.Combine(backup,"location.txt"),root);
 if(entries.Count(x=>Owned(x))==Modes.Length&&Modes.All(m=>entries.Any(x=>Convert.ToString(x["path"])==Prefix+m.Key))){
 foreach(var mode in Modes){string existing=Path.Combine(root,Prefix+mode.Key);if(!File.Exists(Path.Combine(existing,".blendplus-owned")))throw new Exception("扩展资源归属校验失败。");foreach(var originalFrag in Directory.GetFiles(template,"*.frag",SearchOption.AllDirectories).Where(x=>x.IndexOf("shaderGLES",StringComparison.OrdinalIgnoreCase)>=0)){string relative=originalFrag.Substring(template.Length).TrimStart(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);string targetFrag=Path.Combine(existing,relative);if(!File.Exists(targetFrag))throw new Exception("扩展资源不完整，请先恢复。");snapshots[targetFrag]=File.ReadAllBytes(targetFrag);Atomic(targetFrag,Utf(Shader(mode,File.ReadAllText(originalFrag))));}}
 continue;
 }
 if(entries.Any(x=>Owned(x)))throw new Exception("检测到不完整或其他版本的扩展，请先恢复后再安装。");
 int i=0;foreach(var mode in Modes){
 string folder=Path.Combine(root,Prefix+mode.Key);if(Directory.Exists(folder))throw new Exception("目标资源目录已存在，请先恢复："+mode.Name);
 CopyTree(template,folder);File.WriteAllText(Path.Combine(folder,".blendplus-owned"),"BlendPlus v1");made.Add(new KeyValuePair<string,string>(folder,root));
 foreach(var frag in Directory.GetFiles(folder,"*.frag",SearchOption.AllDirectories).Where(x=>x.IndexOf("shaderGLES",StringComparison.OrdinalIgnoreCase)>=0))File.WriteAllText(frag,Shader(mode,File.ReadAllText(frag)),new UTF8Encoding(false));
 entries.Add(new Dictionary<string,object>{{"nameId",mode.Name},{"effectId",(990100+i).ToString()},{"resourceId",(7990000000000000100L+i).ToString()},{"path",Prefix+mode.Key}});i++;
 }
 d["resourceList"]=entries.ToArray();Atomic(list,Utf(Json.Serialize(d)));File.WriteAllText(Path.Combine(backup,"installed.txt"),DateTime.UtcNow.ToString("o"));
 }}catch{foreach(var s in snapshots){try{Atomic(s.Key,s.Value);}catch{}}foreach(var item in made){try{DeleteOwned(item.Key,item.Value);}catch{}}throw;}
 }
 public static void Restore(string exe){Guard();MenuExtension.Restore(exe);RestoreRoots(Roots(exe));}
 public static void RestoreRoots(List<string> roots){foreach(var root in roots){
 string list=Path.Combine(root,"MixMode.json");var d=Read(list);var entries=Entries(d);if(!entries.Any(x=>Owned(x)))continue;
 // Keep unrelated resource-list changes; remove only entries created by this tool.
 d["resourceList"]=entries.Where(x=>!Owned(x)).ToArray();Atomic(list,Utf(Json.Serialize(d)));
 foreach(var m in Modes){string p=Path.Combine(root,Prefix+m.Key);if(Directory.Exists(p)&&File.Exists(Path.Combine(p,".blendplus-owned")))DeleteOwned(p,root);}
 }}
 public static List<InstallInfo> Detect(){
 var found=new HashSet<string>(StringComparer.OrdinalIgnoreCase);Action<string> add=(p)=>{try{if(File.Exists(p)&&File.Exists(Path.Combine(Path.GetDirectoryName(p),"Resources","MixMode","MixMode.json")))found.Add(Path.GetFullPath(p));}catch{}};
 foreach(var saved in Remembered())add(saved);
 foreach(var p in Process.GetProcessesByName("JianyingPro")){try{add(p.MainModule.FileName);}catch{}}
 var dirs=new List<string>{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"JianyingPro","Apps"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"JianyingPro"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"JianyingPro")};
 foreach(var hive in new[]{Registry.CurrentUser,Registry.LocalMachine})foreach(var key in new[]{@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"})try{using(var k=hive.OpenSubKey(key)){if(k==null)continue;foreach(var sub in k.GetSubKeyNames())using(var s=k.OpenSubKey(sub)){if(s==null)continue;string name=Convert.ToString(s.GetValue("DisplayName"));if(name.Contains("剪映")||name.IndexOf("Jianying",StringComparison.OrdinalIgnoreCase)>=0){string loc=Convert.ToString(s.GetValue("InstallLocation"));if(Directory.Exists(loc))dirs.Add(loc);string icon=Convert.ToString(s.GetValue("DisplayIcon")).Trim('"');if(icon.Contains(","))icon=icon.Substring(0,icon.LastIndexOf(','));add(icon);}}}}catch{}
 // Start-menu shortcuts include portable installations saved by the user.
 foreach(var menu in new[]{Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)})try{foreach(var file in Directory.GetFiles(menu,"*.lnk",SearchOption.AllDirectories).Where(x=>x.Contains("剪映")||x.IndexOf("Jianying",StringComparison.OrdinalIgnoreCase)>=0)){
 object shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));object shortcut=shell.GetType().InvokeMember("CreateShortcut",System.Reflection.BindingFlags.InvokeMethod,null,shell,new object[]{file});string target=Convert.ToString(shortcut.GetType().InvokeMember("TargetPath",System.Reflection.BindingFlags.GetProperty,null,shortcut,null));add(target);System.Runtime.InteropServices.Marshal.ReleaseComObject(shortcut);System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);
 }}catch{}
 foreach(var dir in dirs.Distinct(StringComparer.OrdinalIgnoreCase))try{add(Path.Combine(dir,"JianyingPro.exe"));foreach(var child in Directory.GetDirectories(dir))add(Path.Combine(child,"JianyingPro.exe"));}catch{}
 foreach(var p in found)Remember(p,false);
 return found.Select(x=>new InstallInfo{Exe=x}).OrderByDescending(x=>x.Version).ToList();
 }

 public static string Shader(Mode m,string original){
 int begin=original.IndexOf("vec4 MultiplyBlendStraight(",StringComparison.Ordinal);
 int tail=original.IndexOf("void CornerPositioning(",StringComparison.Ordinal);
 if(tail<0)tail=original.IndexOf("void main",StringComparison.Ordinal);
 if(begin<0||tail<=begin)throw new Exception("此版本的着色器函数结构未适配。");
 string math=@"
float lum(vec3 c){return dot(c,vec3(0.3,0.59,0.11));}
float sat(vec3 c){return max(max(c.r,c.g),c.b)-min(min(c.r,c.g),c.b);}
vec3 clipColor(vec3 c){float l=lum(c);float n=min(min(c.r,c.g),c.b);float x=max(max(c.r,c.g),c.b);if(n<0.0)c=vec3(l)+(c-vec3(l))*l/max(l-n,0.000001);if(x>1.0)c=vec3(l)+(c-vec3(l))*(1.0-l)/max(x-l,0.000001);return c;}
vec3 setLum(vec3 c,float l){return clipColor(c+vec3(l-lum(c)));}
vec3 setSat(vec3 c,float s){float lo=min(min(c.r,c.g),c.b);float hi=max(max(c.r,c.g),c.b);return hi>lo?(c-vec3(lo))*s/(hi-lo):vec3(0.0);}
float dv(float b,float f){return f<=0.0?1.0:min(b/f,1.0);}
float burn(float b,float f){if(b>=1.0)return 1.0;if(f<=0.0)return 0.0;return 1.0-min((1.0-b)/f,1.0);}
float dodge(float b,float f){if(b<=0.0)return 0.0;if(f>=1.0)return 1.0;return min(b/(1.0-f),1.0);}
float vivid(float b,float f){return f<0.5?burn(b,2.0*f):dodge(b,2.0*f-1.0);}
float pin(float b,float f){return f<0.5?min(b,2.0*f):max(b,2.0*f-1.0);}
vec3 blend(vec3 b,vec3 f){return "+m.Expr+@";}
vec4 MultiplyBlendStraight(vec4 fg,vec4 bg){
 float a=fg.a+bg.a-fg.a*bg.a;
 vec3 c=fg.rgb*fg.a*(1.0-bg.a)+bg.rgb*bg.a*(1.0-fg.a)+fg.a*bg.a*blend(bg.rgb,fg.rgb);
 return vec4(c/max(a,Eps),a);
}
vec4 MultiplyBlendPreMul(vec4 fg,vec4 bg){
 float a=fg.a+bg.a-fg.a*bg.a;
 vec3 f=fg.a>0.0?fg.rgb/max(fg.a,Eps):vec3(0.0);
 vec3 b=bg.a>0.0?bg.rgb/max(bg.a,Eps):vec3(0.0);
 vec3 c=fg.rgb*(1.0-bg.a)+bg.rgb*(1.0-fg.a)+fg.a*bg.a*blend(b,f);
 return vec4(c,a);
}
";
 // Keep the host's varying types, projective coordinates and alpha conversion unchanged.
 return original.Substring(0,begin)+math+original.Substring(tail);
 }

}
class MainForm:Form {
 ComboBox versions=new ComboBox();Label status=new Label();Button install=new Button(),restore=new Button();bool scanning;
 public MainForm(){
 Text="剪映混合模式扩展";ClientSize=new Size(580,188);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=true;StartPosition=FormStartPosition.CenterScreen;AutoScaleMode=AutoScaleMode.Dpi;Font=new Font("Microsoft YaHei UI",9F);BackColor=SystemColors.Window;
 versions.SetBounds(20,22,428,30);versions.DropDownStyle=ComboBoxStyle.DropDownList;versions.DropDownWidth=850;versions.SelectedIndexChanged+=(s,e)=>{if(!scanning){var item=versions.SelectedItem as InstallInfo;if(item!=null)try{Engine.Remember(item.Exe,true);}catch{}Inspect();}};Controls.Add(versions);
 var browse=new Button{Text="浏览…",Location=new Point(460,21),Size=new Size(100,30)};browse.Click+=(s,e)=>{using(var dlg=new OpenFileDialog{Title="选择剪映",Filter="剪映|JianyingPro.exe",CheckFileExists=true})if(dlg.ShowDialog()==DialogResult.OK){try{Engine.Remember(dlg.FileName,true);Scan(dlg.FileName);}catch(Exception err){Say(err.Message,true);}}};Controls.Add(browse);
 status.SetBounds(20,69,540,40);status.Text="选择版本，关闭剪映后点击安装。";Controls.Add(status);
 var refresh=new Button{Text="刷新",Location=new Point(20,132),Size=new Size(88,32)};refresh.Click+=(s,e)=>Scan(null);Controls.Add(refresh);
 install.Text="安装";install.SetBounds(350,132,100,32);install.Click+=(s,e)=>Run(false);Controls.Add(install);
 restore.Text="恢复";restore.SetBounds(460,132,100,32);restore.Click+=(s,e)=>Run(true);Controls.Add(restore);
 Shown+=(s,e)=>Scan(null);
 }
 void Say(string text,bool error){status.Text=text;status.ForeColor=error?Color.Firebrick:SystemColors.ControlText;}
 void Scan(string requested){
 var old=versions.SelectedItem as InstallInfo;string previous=requested??(old==null?null:old.Exe);
 scanning=true;try{var found=Engine.Detect();var preferred=Engine.Preferred(found,previous);versions.Items.Clear();foreach(var v in found)versions.Items.Add(v);if(preferred!=null){versions.SelectedItem=preferred;try{Engine.Remember(preferred.Exe,true);}catch{}}}catch(Exception e){Say(e.Message,true);}finally{scanning=false;}Inspect();
 }
 void Inspect(){
 install.Enabled=restore.Enabled=false;var item=versions.SelectedItem as InstallInfo;if(item==null){Say("未找到剪映，点击浏览选择。",false);return;}
 try{Engine.Validate(Engine.ResourceRoot(item.Exe));var entries=Engine.Entries(Engine.Read(Path.Combine(Engine.ResourceRoot(item.Exe),"MixMode.json")));int count=entries.Count(x=>Engine.Owned(x));Say(count==Engine.Modes.Length?"已安装 15 种模式。":"关闭剪映后，点击安装添加 15 种模式。",false);install.Text=count==Engine.Modes.Length?"重新安装":"安装";install.Enabled=true;restore.Enabled=count>0;}catch(Exception e){Say(e.Message,true);}
 }
 void Run(bool undo){var item=versions.SelectedItem as InstallInfo;if(item==null)return;try{UseWaitCursor=true;install.Enabled=restore.Enabled=false;Engine.Remember(item.Exe,true);if(undo)Engine.Restore(item.Exe);else Engine.Install(item.Exe);Inspect();Say(undo?"已恢复 "+item.Version+"，重新打开剪映即可。":"已安装到 "+item.Version+"，重新打开剪映即可。",false);}catch(UnauthorizedAccessException){Inspect();Say("请右键以管理员身份运行工具。",true);}catch(Exception e){Inspect();Say(e.Message,true);}finally{UseWaitCursor=false;}}
 [STAThread] static void Main(string[] args){if(args.Length>0&&args[0]=="--self-test"){SelfTest.Run(args[1]);return;}Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new MainForm());}
}
static class SelfTest {
 static string TestShader(bool projective){return "precision mediump float; uniform sampler2D baseTex; uniform sampler2D _MainTex; uniform int blendAlphaState,fgAlphaState,bgAlphaState; varying "+(projective?"vec3":"vec2")+" uv0; varying vec2 uv1; const float Eps=1e-6; vec4 MultiplyBlendStraight(vec4 f,vec4 b){return f;} vec4 MultiplyBlendPreMul(vec4 f,vec4 b){return f;} void main(){vec2 p="+(projective?"uv0.xy/uv0.z":"uv0")+";gl_FragColor=texture2D(_MainTex,p);}";}
 public static void Run(string output){var root=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output)),"BlendPlus-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);try{
 var template=Path.Combine(root,"stock");Directory.CreateDirectory(Path.Combine(template,"Library","shaderGLES"));Directory.CreateDirectory(Path.Combine(template,"xshader"));foreach(var f in new[]{"config.json","content.json","anim.prefab","blend.material","xshader/blend.xshader"})File.WriteAllText(Path.Combine(template,f),"{}");File.WriteAllText(Path.Combine(template,"Library","shaderGLES","stock.frag"),TestShader(false));
 File.WriteAllText(Path.Combine(root,"MixMode.json"),"{\"resourceList\":[{\"nameId\":\"multiply_blend_mode\",\"path\":\"stock\"}]}");Engine.Store=Path.Combine(root,"backups");Engine.SettingsPath=Path.Combine(root,"state","installations.json");Engine.Validate(root);Engine.InstallRoots(new List<string>{root});if(Engine.Entries(Engine.Read(Path.Combine(root,"MixMode.json"))).Count!=16)throw new Exception("Install count");Engine.InstallRoots(new List<string>{root});if(Engine.Entries(Engine.Read(Path.Combine(root,"MixMode.json"))).Count!=16)throw new Exception("Idempotence");var testList=Engine.Read(Path.Combine(root,"MixMode.json"));var testEntries=Engine.Entries(testList);testEntries.Add(new Dictionary<string,object>{{"nameId","foreign"},{"path","foreign"}});testList["resourceList"]=testEntries.ToArray();File.WriteAllText(Path.Combine(root,"MixMode.json"),Engine.Json.Serialize(testList));Engine.RestoreRoots(new List<string>{root});if(Engine.Entries(Engine.Read(Path.Combine(root,"MixMode.json"))).Count!=2)throw new Exception("Restore removed foreign entries");if(Directory.GetDirectories(root).Any(x=>Path.GetFileName(x).StartsWith(Engine.Prefix)))throw new Exception("Restore left owned resources");var portable=Path.Combine(root,"portable");Directory.CreateDirectory(Path.Combine(portable,"Resources","MixMode"));string rememberedExe=Path.Combine(portable,"JianyingPro.exe");File.WriteAllText(rememberedExe,"test fixture");File.WriteAllText(Path.Combine(portable,"Resources","MixMode","MixMode.json"),"{}");Engine.Remember(rememberedExe,true);var firstScan=Engine.Detect();if(!firstScan.Any(x=>String.Equals(x.Exe,rememberedExe,StringComparison.OrdinalIgnoreCase)))throw new Exception("Closed portable version lost");if(Engine.Preferred(firstScan,null).Exe!=rememberedExe)throw new Exception("Selection lost");var secondScan=Engine.Detect();if(Engine.Preferred(secondScan,rememberedExe).Exe!=rememberedExe)throw new Exception("Refresh changed selection");if(Engine.Modes.Select(m=>m.Key).Distinct().Count()!=15)throw new Exception("Duplicate modes");foreach(var m in Engine.Modes){var shader=Engine.Shader(m,TestShader(false));var projective=Engine.Shader(m,TestShader(true));if(!projective.Contains("varying vec3 uv0;")||!projective.Contains("uv0.xy/uv0.z"))throw new Exception("Projective UV lost");if(!shader.Contains("void main()")||shader.Contains("NaN"))throw new Exception("Shader failure");}File.WriteAllText(output,"PASS: installation; repeated installation; backup; removal; preservation of unrelated entries; owned directory cleanup; remembered portable installation while not running; persisted selection; refresh selection; 15 unique modes; resource schema validation; GLSL generation.");
 }catch(Exception e){File.WriteAllText(output,"FAIL: "+e);}finally{Directory.Delete(root,true);}}
}


