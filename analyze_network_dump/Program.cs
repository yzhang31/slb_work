using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace analyze_network_dump
{
    class Program
    {
    
        enum InputOutputType : int
        {
            Parameter = 0,
            Channel,
            Survey,
            CaliCoeffcient,
            Result,
            IntervalSet
        }

        class DataRelation
        {
            public string OSDD;
            public InputOutputType Type;
            public bool IsServiceSelection = false;
        }

        class ModuleInfo
        {
            public string DomainType;
            public string ProgID;
            public string Type;
            public string ToolPath;
            public List<DataRelation> Inputs;
            public List<DataRelation> Outputs;

            public string ServiceParamemterList()
            {
                var plist = from p in Inputs
                            where p.IsServiceSelection == true
                            select p.OSDD;

                return ComposeNameList(plist);
                
            }
            public string NormalParamemterList()
            {
                var plist = from p in Inputs
                            where p.IsServiceSelection == false && p.Type == InputOutputType.Parameter
                            select p.OSDD;

                return ComposeNameList(plist);
            }

            private static string ComposeNameList(IEnumerable<string> plist)
            {
                string result = string.Empty;
                foreach (var item in plist)
                {
                    Trace.Assert(!String.IsNullOrWhiteSpace(item));
                    result += item + "/";
                }
                return result;
            }

            public string OutputChannelList()
            {
                var plist = from p in Outputs
                            where p.Type == InputOutputType.Channel
                            select p.OSDD;

                return ComposeNameList(plist);
            }

            public bool HasOutputs()
            {
                return (Outputs.Count > 0);
            }
        }

        static void Main(string[] args)
        {

            if (args.Count() != 2)
            {
                Console.WriteLine(@"sample: computation_log_analyzer.exe d:\temp d:\module_type.csv");
                return;
            }

            string dirPath = args[0];
            string outputfile = args[1];

            IEnumerable<string> dump_files = Directory.GetFiles(dirPath, "*dump*.xml", SearchOption.AllDirectories);

            SortedList<string, ModuleInfo> sorted_all_modules = new SortedList<string, ModuleInfo>();
            foreach (var path in dump_files)
            {
                Console.WriteLine("Processing: " + path);

                IList<ModuleInfo> all_modules = LoadAllModuels(path);

                if (all_modules.Count == 0)
                {
                    Console.WriteLine("     ERROR. No valid module tool info.");
                }

                foreach (var module in all_modules)
                {
                    if (!sorted_all_modules.ContainsKey(module.ProgID))
                    {
                        sorted_all_modules.Add(module.ProgID, module);
                    }
                }

            }
            output(sorted_all_modules.Values, outputfile);
        }

        private static IList<ModuleInfo> LoadAllModuels(string filePath)
        {
            IList<ModuleInfo> all_modules = new List<ModuleInfo>();
            try
            {
                var xml = XDocument.Load(filePath);

                var domains =
                    from domain in xml.Descendants()
                    where domain.Name == "Domain"
                    select domain;

                all_modules = ReadDomainModuleInfo(domains);
            }
            catch(System.Xml.XmlException e)
            {
                Console.WriteLine("Exception while loading xml file: %s" + filePath);

            }
            return all_modules;
        }

        static IList<ModuleInfo> ReadDomainModuleInfo(IEnumerable<XElement> domains)
        {
            List<ModuleInfo> all_modules = new List<ModuleInfo>();

            string domainType = string.Empty;
            foreach (var domain in domains)
            {
                var type = from d in domain.Elements()
                           where d.Name == "DomainType"
                           select d.Value;

                domainType = type.First();
                var moduleInfos = from e in domain.Descendants()
                                  where e.Name == "ModuleEntry"
                                  select e;

                foreach (var m in moduleInfos)
                {
                    if (m.Descendants("ToolPath").Any())
                    {
                        ModuleInfo mi = ReadModuleInfo(m);
                        mi.DomainType = domainType;
                        all_modules.Add(mi);
                    }
                }
            }
            return all_modules;
        }

            static ModuleInfo ReadModuleInfo(XElement m)
        {
            ModuleInfo minfo = new ModuleInfo();
            minfo.ProgID = m.Element("Name").Value;
            minfo.ToolPath = m.Element("ToolPath").Value;
            minfo.Inputs = new List<DataRelation>();
            minfo.Outputs = new List<DataRelation>();
            minfo.Type = m.Element("ModuleType").Value;

            var inputs = from p in m.Elements("Inputs")
                         from sp in p.Elements("Input")
                         select sp;

            foreach (var item in inputs)
            {
                DataRelation dr = GetRelation(item);
                if (dr.Type == InputOutputType.IntervalSet)
                {
                    continue;
                }
                Trace.Assert(!string.IsNullOrWhiteSpace(dr.OSDD));
                minfo.Inputs.Add(dr);
            }

            //Trace.Assert(minfo.Inputs.Count == inputs.Count());


            var outputs = from p in m.Elements("Outputs")
                          from sp in p.Elements("Output")
                          select sp;

            foreach (var item in outputs)
            {
                DataRelation dr = GetRelation(item);

                if (dr.Type == InputOutputType.IntervalSet)
                {
                    continue;
                }

                Trace.Assert(!string.IsNullOrWhiteSpace(dr.OSDD));
                minfo.Outputs.Add(dr);
            }

            Trace.Assert(minfo.Outputs.Count == outputs.Count());
            return minfo;
        }

        private static DataRelation GetRelation(XElement singleRelation)
        {
            DataRelation dr = new DataRelation();

            foreach (InputOutputType val in Enum.GetValues(typeof(InputOutputType)))
            {
                if (singleRelation.Element("OSDD").Value.Contains(val.ToString()))
                {
                    dr.OSDD = singleRelation.Element("OSDD").Value.Split('-')[0].Trim();
                    dr.Type = val;
                    if (singleRelation.Element("IsSeviceSelector") != null && singleRelation.Element("IsSeviceSelector").Value == "True")
                    {
                        dr.IsServiceSelection = true;
                    }
                }
            }
            return dr;
        }

        private static void output(IList<ModuleInfo> all_modules, string outputfile)
        {

            using (StreamWriter writer = File.CreateText(outputfile))
            {
                writer.WriteLine("Tool,Domain,ModuleName,ModuleType,ServiceParameter,NormalParameter,HasOutput,OutputChannel");
                foreach (var item in all_modules)
                {
                    writer.WriteLine(item.ToolPath + "," + item.DomainType + "," + item.ProgID + ","
                        + item.Type + "," + item.ServiceParamemterList() + "," 
                        + item.NormalParamemterList() + "," + item.HasOutputs().ToString() 
                        + "," + item.OutputChannelList());
                }
            }

        }
    }
}
