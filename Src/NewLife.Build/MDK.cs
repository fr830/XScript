﻿using System;
using System.IO;
using System.Text;
using Microsoft.Win32;
using NewLife.Log;
using NewLife.Web;

namespace NewLife.Build
{
    /// <summary>MDK环境</summary>
    public class MDK : Builder
    {
        /// <summary>是否使用最新的MDK 6.4</summary>
        public Boolean CLang { get; set; }

        #region 初始化
        private static MDKLocation location = new MDKLocation();

        /// <summary>初始化</summary>
        public MDK()
        {
            Name = "MDK";

            Version = location.Version;
            ToolPath = location.ToolPath;
        }
        #endregion

        /// <summary>初始化</summary>
        /// <param name="addlib"></param>
        /// <returns></returns>
        public override Boolean Init(Boolean addlib)
        {
            var root = ToolPath;
            if (CLang)
            {
                root = ToolPath.CombinePath("ARMCLANG\\bin").GetFullPath();
                if (!Directory.Exists(root)) root = ToolPath.CombinePath("ARMCC\\bin").GetFullPath();
            }
            else
            {
                // CLang编译器用来检查语法非常棒，但是对代码要求很高，我们有很多代码需要改进，暂时不用
                root = ToolPath.CombinePath("ARMCC\\bin").GetFullPath();
            }

            Complier = root.CombinePath("armcc.exe");
            if (!File.Exists(Complier)) Complier = root.CombinePath("armclang.exe");
            Asm = root.CombinePath("armasm.exe");
            Link = root.CombinePath("armlink.exe");
            Ar = root.CombinePath("armar.exe");
            FromELF = root.CombinePath("fromelf.exe");
            IncPath = root.CombinePath("..\\include").GetFullPath();
            LibPath = root.CombinePath("..\\lib").GetFullPath();

            return base.Init();
        }

        #region 主要编译方法
        /// <summary>获取编译用的命令行</summary>
        /// <param name="cpp">是否C++</param>
        /// <returns></returns>
        public override String GetCompileCommand(Boolean cpp)
        {
            var sb = new StringBuilder();
            /*
             * -c --cpu Cortex-M0 -D__MICROLIB -g -O3 --apcs=interwork --split_sections -I..\Lib\inc -I..\Lib\CMSIS -I..\SmartOS
             * -DSTM32F030 -DUSE_STDPERIPH_DRIVER -DSTM32F0XX -DGD32 -o ".\Obj\*.o" --omf_browse ".\Obj\*.crf" --depend ".\Obj\*.d"
             *
             * -c --cpu Cortex-M3 -D__MICROLIB -g -O0 --apcs=interwork --split_sections -I..\STM32F1Lib\inc -I..\STM32F1Lib\CMSIS -I..\SmartOS
             * -DSTM32F10X_HD -DDEBUG -DUSE_FULL_ASSERT -o ".\Obj\*.o" --omf_browse ".\Obj\*.crf" --depend ".\Obj\*.d"
             */

            sb.Append("-c");
            if (cpp) sb.Append(" --cpp11");
            //sb.AppendFormat(" --cpu {0} -D__MICROLIB -g -O{1} --exceptions --apcs=interwork --split_sections", CPU, Debug ? 0 : 3);
            sb.AppendFormat(" --cpu {0} -D__MICROLIB -g -O{1} --apcs=interwork --split_sections", CPU, Debug ? 0 : 3);
            sb.Append(" --multibyte_chars --locale \"chinese\"");
            // arm_linux 需要编译器授权支持
            //if(Linux) sb.Append(" --arm_linux");
            // --signed_chars
            if (Linux) sb.Append(" --enum_is_int --wchar32");

            if (Debug) sb.Append(" -DDEBUG -DUSE_FULL_ASSERT");
            if (Tiny) sb.Append(" -DTINY");
            foreach (var item in Defines)
            {
                sb.AppendFormat(" -D{0}", item);
            }

            foreach (var item in ExtCompiles)
            {
                sb.AppendFormat(" {0}", item.Trim());
            }

            return sb.ToString();
        }

        /// <summary>编译输出</summary>
        /// <param name="file"></param>
        protected virtual String OnCompile(String file)
        {
            var sb = new StringBuilder();
            var objName = GetObjPath(file);
            if (Preprocess)
            {
                sb.AppendFormat(" -E");
                sb.AppendFormat(" -o \"{0}.{1}\" --omf_browse \"{0}.crf\" --depend \"{0}.d\"", objName, Path.GetExtension(file).TrimStart("."));
            }
            else
                sb.AppendFormat(" -o \"{0}.o\" --omf_browse \"{0}.crf\" --depend \"{0}.d\"", objName);
            sb.AppendFormat(" -c \"{0}\"", file);

            return sb.ToString();
        }

        /// <summary>汇编程序</summary>
        /// <param name="file"></param>
        /// <returns></returns>
        protected override String OnAssemble(String file)
        {
            var lstName = GetListPath(file);
            var objName = GetObjPath(file);

            var sb = new StringBuilder();
            sb.AppendFormat("--cpu {0} -g --apcs=interwork --pd \"__MICROLIB SETA 1\"", CPU);
            //sb.AppendFormat(" --pd \"{0} SETA 1\"", Flash);

            //if (GD32) sb.Append(" --pd \"GD32 SETA 1\"");
            foreach (var item in Defines)
            {
                sb.AppendFormat(" --pd \"{0} SETA 1\"", item);
            }
            if (Debug) sb.Append(" --pd \"DEBUG SETA 1\"");
            if (Tiny) sb.Append(" --pd \"TINY SETA 1\"");

            sb.AppendFormat(" --list \"{0}.lst\" --xref -o \"{1}.o\" --depend \"{1}.d\"", lstName, objName);
            sb.AppendFormat(" \"{0}\"", file);

            return sb.ToString();
        }
        #endregion

        /// <summary>初始化关键字</summary>
        protected override void InitWord()
        {
            base.InitWord();

            var ss = Words;
            ss["Fatal error"] = "致命错误";
            ss["fatal error"] = "致命错误";
            ss["Could not open file"] = "无法打开文件";
            ss["No such file or directory"] = "文件或目录不存在";
            ss["Undefined symbol"] = "未定义标记";
            ss["referred from"] = "引用自";
            ss["Program Size"] = "程序大小";
            ss["Finished"] = "程序大小";
            ss["declared at"] = "声明于";
            ss["required for copy that was eliminated"] = "已淘汰";
            ss["it is a deleted function"] = "函数已标记为删除";
            ss["be referenced"] = "被引用";
            ss["the format string ends before this argument"] = "格式字符串参数不足";
            ss["has already been declared in the current scope"] = "已在当前区域中定义";
            ss["more than one operator"] = "多于一个运算符";
            ss["matches these operands"] = "匹配该操作";
            ss["operand types are"] = "操作类型";
            ss["no instance of overloaded function"] = "没有函数";
            ss["matches the argument list"] = "匹配参数列表";
            ss["argument types are"] = "参数类型是";
            ss["object type is"] = "对象类型是";
            ss["initial value of reference to non-const must be an lvalue"] = "非常量引用初值必须是左值";
            ss["too many arguments in function call"] = "函数调用参数过多";
            ss["cannot be initialized with a value of type"] = "不能初始化为类型";
            ss["a reference of type"] = "引用类型";
            ss["connot be assigned to an entity of type"] = "不能赋值给类型";
            ss["detected during instantiation of"] = "在检测实例化";
            ss["not const-qualified"] = "非常量约束";
            ss["no instance of constructor"] = "没有构造函数";
            ss["is undefined"] = "未定义";
            ss["declaration is incompatible with"] = "声明不兼容";
            ss["is inaccessible"] = "不可访问";
            ss["expression must have class type"] = "表达式必须是类";
            ss["argument is incompatible with corresponding format string conversion"] = "格式化字符串不兼容参数";
            ss["no suitable constructor exists to convert from"] = "没有合适的构造函数去转换";
            ss["nonstandard form for taking the address of a member function"] = "获取成员函数地址不标准（&Class::Method）";
            ss["argument of type"] = "实参类型";
            ss["is incompatible with parameter of type"] = "不兼容形参类型";
        }
    }

    /// <summary>MDK 6.0，采用LLVM技术的CLang编译器</summary>
    public class MDK6 : MDK
    {
        /// <summary>实例化</summary>
        public MDK6()
        {
            Name = "MDK6";
            CLang = true;
        }

        #region 主要编译方法
        /// <summary>获取编译用的命令行</summary>
        /// <param name="cpp">是否C++</param>
        /// <returns></returns>
        public override String GetCompileCommand(Boolean cpp)
        {
            var sb = new StringBuilder();
            /*
             * -xc --target=arm-arm-none-eabi -mcpu=cortex-m3 -c
             * -funsigned-char
             * -D__MICROLIB -gdwarf-3 -O0 -ffunction-sections
             * -I ..\Lib\inc -I ..\Lib\CMSIS -I ..\SmartOS -I ..\SmartOS\Core -I ..\SmartOS\Device
             * -I ..\SmartOS\Kernel
             * -D__UVISION_VERSION="520" -DSTM32F10X_HD -DSTM32F1 -DDEBUG -DUSE_FULL_ASSERT -DR24
             * -o .\Obj\*.o -MD
             */

            sb.Append("-xc++");
            //if (file.EndsWithIgnoreCase(".cpp")) sb.Append(" -std=gnu++11");
            if (cpp) sb.Append(" -std=c++14");
            sb.Append(" --target=arm-arm-none-eabi -funsigned-char -MD");
            sb.AppendFormat(" -mcpu={0} -D__MICROLIB -gdwarf-3 -O{1} -ffunction-sections", CPU.ToLower(), Debug ? 0 : 3);
            sb.Append(" -Warmcc-pragma-arm");

            if (Debug) sb.Append(" -DDEBUG -DUSE_FULL_ASSERT");
            if (Tiny) sb.Append(" -DTINY");
            foreach (var item in Defines)
            {
                sb.AppendFormat(" -D{0}", item);
            }

            foreach (var item in ExtCompiles)
            {
                sb.AppendFormat(" {0}", item.Trim());
            }

            foreach (var item in Includes)
            {
                sb.AppendFormat(" -I{0}", item);
            }
            //if(Directory.Exists(IncPath)) sb.AppendFormat(" -I{0}", IncPath);

            return sb.ToString();
        }
        #endregion
    }

    class MDKLocation
    {
        #region 属性
        /// <summary>版本</summary>
        public String Version { get; set; }

        /// <summary>工具目录</summary>
        public String ToolPath { get; set; }
        #endregion

        public MDKLocation()
        {
            #region 从注册表获取目录和版本
            if (String.IsNullOrEmpty(ToolPath))
            {
                var reg = Registry.LocalMachine.OpenSubKey("Software\\Keil\\Products\\MDK");
                if (reg == null) reg = Registry.LocalMachine.OpenSubKey("Software\\Wow6432Node\\Keil\\Products\\MDK");
                if (reg != null)
                {
                    ToolPath = reg.GetValue("Path") + "";
                    //var s = (reg.GetValue("Version") + "").Trim('V', 'v', 'a', 'b', 'c');
                    //var ss = s.SplitAsInt(".");
                    //Version = new Version(ss[0], ss[1]);
                    Version = reg.GetValue("Version") + "";

                    if (!String.IsNullOrEmpty(ToolPath)) XTrace.WriteLine("注册表 {0} {1}", ToolPath, Version);
                }
            }
            #endregion

            #region 扫描所有根目录，获取MDK安装目录
            //if (String.IsNullOrEmpty(ToolPath))
            {
                foreach (var item in DriveInfo.GetDrives())
                {
                    if (!item.IsReady) continue;

                    var p = Path.Combine(item.RootDirectory.FullName, "Keil\\ARM");
                    if (Directory.Exists(p))
                    {
                        var ver = GetVer(p);
                        if (ver.CompareTo(Version) > 0)
                        {
                            ToolPath = p;
                            Version = ver;

                            XTrace.WriteLine("本地 {0} {1}", p, ver);
                        }
                    }
                }
            }
            if (Version.ToLower().CompareTo("v5.17") < 0)
            {
                XTrace.WriteLine("版本 {0} 太旧，准备更新", Version);

                var url = "http://www.newlifex.com/showtopic-1456.aspx";
                var client = new WebClientX(true, true);
                client.Log = XTrace.Log;
                var dir = Environment.SystemDirectory.CombinePath("..\\..\\Keil").GetFullPath();
                var file = client.DownloadLinkAndExtract(url, "MDK", dir);
                var p = dir.CombinePath("ARM");
                if (Directory.Exists(p))
                {
                    var ver = GetVer(p);
                    if (ver.CompareTo(Version) > 0)
                    {
                        ToolPath = p;
                        Version = ver;
                    }
                }
            }
            if (String.IsNullOrEmpty(ToolPath)) throw new Exception("无法获取MDK安装目录！");
            #endregion
        }

        String GetVer(String path)
        {
            var p = Path.Combine(path, "..\\Tools.ini");
            if (File.Exists(p))
            {
                foreach (var item in File.ReadAllLines(p))
                {
                    if (String.IsNullOrEmpty(item)) continue;
                    if (item.StartsWith("VERSION=", StringComparison.OrdinalIgnoreCase))
                    {
                        //var s = item.Substring("VERSION=".Length).Trim().Trim('V', 'v', 'a', 'b', 'c');
                        //var ss = s.SplitAsInt(".");
                        //return new Version(ss[0], ss[1]);
                        //break;

                        return item.Substring("VERSION=".Length).Trim();
                    }
                }
            }

            return "";
        }
    }
}