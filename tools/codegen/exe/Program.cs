// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may
// not use these files except in compliance with the License. You may obtain
// a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
// WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
// License for the specific language governing permissions and limitations
// under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml.Serialization;
using System.Globalization;
using System.Diagnostics;

namespace CodeGen
{
    public class OutputDataTypes
    {
        public OutputDataTypes()
        {
            m_types = new List<OutputtableType>();
        }

        public void AddEnum(Enum e)
        {
            m_types.Add(e);
        }

        public void AddStruct(Struct s)
        {
            m_types.Add(s);
        }

        public void OutputCode(Dictionary<string, QualifiableType> typeDictionary, Formatter idlFile)
        {
            OutputLeadingComment(idlFile);

            var sortedTypes = m_types.OrderBy(x => x.Namespace).ThenBy(x => x.GetType()).ThenBy(x => x.ProjectedName);

            string currentNamespace = null;

            foreach (var type in sortedTypes)
            {
                UpdateNamespace(idlFile, ref currentNamespace, type.Namespace);
                type.OutputCode(typeDictionary, idlFile);
            }

            UpdateNamespace(idlFile, ref currentNamespace, null);
        }

        public void UpdateNamespace(Formatter idlFile, ref string currentNamespace, string newNamespace)
        {
            if (currentNamespace != newNamespace)
            {
                if (currentNamespace != null)
                {
                    // end the previous namespace
                    idlFile.Unindent();
                    idlFile.WriteLine("} // " + currentNamespace);
                    idlFile.WriteLine("");
                }

                currentNamespace = newNamespace;

                if (newNamespace != null)
                {
                    idlFile.WriteLine("namespace " + newNamespace);
                    idlFile.WriteLine("{");
                    idlFile.Indent();
                }
            }

        }

        public static void OutputLeadingComment(Formatter output)
        {
            output.WriteLine("// Copyright (c) Microsoft Corporation. All rights reserved.");
            output.WriteLine("//");
            output.WriteLine("// Licensed under the Apache License, Version 2.0 (the \"License\"); you may");
            output.WriteLine("// not use these files except in compliance with the License. You may obtain");
            output.WriteLine("// a copy of the License at http://www.apache.org/licenses/LICENSE-2.0");
            output.WriteLine("//");
            output.WriteLine("// Unless required by applicable law or agreed to in writing, software");
            output.WriteLine("// distributed under the License is distributed on an \"AS IS\" BASIS, WITHOUT");
            output.WriteLine("// WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the");
            output.WriteLine("// License for the specific language governing permissions and limitations");
            output.WriteLine("// under the License.");
            output.WriteLine();

            output.WriteLine("// This file was automatically generated. Please do not edit it manually.");
            output.WriteLine();
        }

        List<OutputtableType> m_types;
    }
    
    public class Program
    {
        static void Main(string[] args)
        {
            var inputDir = FindInputDirectory();

            ProcessedInputFiles processedInputFiles = ProcessInputFiles(inputDir);

            GenerateCode(processedInputFiles, GetDefaultOutputLocation(inputDir));
            GenerateEffectsCode(inputDir, processedInputFiles.TypeDictionary, GetDefaultEffectsOutputLocation(inputDir));
        }
        
        public static string GetDefaultOutputLocation(string inputDir)
        {
            return Path.GetFullPath(Path.Combine(inputDir, "..", "..", "..", "winrt", "lib"));
        }

        public static string GetDefaultEffectsOutputLocation(string inputDir)
        {
            return Path.GetFullPath(Path.Combine(inputDir, "..", "..", "..", "winrt", "lib", "effects", "generated"));
        }

        public static List<string> GetInputFileList()
        {
            List<string> files = new List<string>();
            files.Add("apiref/D2DTypes.xml");
            files.Add("apiref/D2DTypes2.xml");
            files.Add("apiref/D2DEffectAuthor.xml");
            files.Add("apiref/D2DTypes3.xml");
            return files;
        }

        public static void GenerateEffectsCode(string inputDir, Dictionary<string, QualifiableType> typeDictionary, string outputDir)
        {
            String inputEffectsDir = Path.Combine(inputDir, "apiref/effects");
            EffectGenerator.OutputEffects(inputEffectsDir, typeDictionary, outputDir);
        }

        public struct ProcessedInputFiles
        {
            public string FilenameBase;
            public Dictionary<string, QualifiableType> TypeDictionary;
            public OutputDataTypes OutputDataTypes;
        }

        public static ProcessedInputFiles ProcessInputFiles(string inputDir)
        {
            ProcessedInputFiles result = new ProcessedInputFiles();

            List<string> files = GetInputFileList();

            Overrides.XmlBindings.Settings overridesXmlData = XmlBindings.Utilities.LoadXmlData<Overrides.XmlBindings.Settings>(inputDir, "Settings.xml");
            Formatter.Prefix = overridesXmlData.Prefix.Value;

            result.FilenameBase = overridesXmlData.FilenameBase.Value;

            List<D2DTypes> typeDocuments = new List<D2DTypes>();
            result.TypeDictionary = new Dictionary<string, QualifiableType>();
            result.OutputDataTypes = new OutputDataTypes();
            foreach (string fileName in files)
            {
                XmlBindings.D2DTypes xmlDocument = XmlBindings.Utilities.LoadXmlData<XmlBindings.D2DTypes>(inputDir, fileName);
                typeDocuments.Add(new D2DTypes(xmlDocument, overridesXmlData, result.TypeDictionary, result.OutputDataTypes));
            }

            return result;
        }

        public static void GenerateCode(ProcessedInputFiles inputFiles, string outputDir)
        {

            Directory.CreateDirectory(outputDir);
            using (Formatter idlStreamWriter = new Formatter(Path.Combine(outputDir, inputFiles.FilenameBase + ".codegen.idl")))
            {
                inputFiles.OutputDataTypes.OutputCode(inputFiles.TypeDictionary, idlStreamWriter);
            }
        }

        //
        // The input directory, which contains the various XML spec files, can be found by checking some known locations.
        //
        public static string FindInputDirectory()
        {
            string[] candidates = new string[] {
                ".",
                "codegen/exe",
                "../../../../tools/codegen/exe",
            };

            foreach (var candidate in candidates)
            {
                var normalizedCandidate = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, candidate));
                if (IsInputDirectory(normalizedCandidate))
                    return normalizedCandidate;
            }

            throw new FileNotFoundException("Couldn't find the codegen source directory");
        }

        //
        // The input directory is that one that contains Settings.xml.
        //
        static bool IsInputDirectory(string directory)
        {
            return File.Exists(Path.Combine(directory, "Settings.xml"));
        }

    }
}
