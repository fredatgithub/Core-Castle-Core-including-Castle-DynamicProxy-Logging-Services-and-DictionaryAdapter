// Copyright 2004-2021 Castle Project - http://www.castleproject.org/
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;

namespace Explicit.NuGet.Versions
{
	class Program
	{
		static void Main(string[] arguments)
		{
			// args[0] = dossier contenant les .nupkg
			// args[1] = prefixe du package NuGet dont tu veux forcer la version explicite
			var packageDiscoveryDirectory = Path.Combine(Environment.CurrentDirectory, arguments[0]);
			var packageDiscoverDirectoryInfo = new DirectoryInfo(packageDiscoveryDirectory);
			var packageMetaData = ReadNuspecFromPackages(packageDiscoverDirectoryInfo);
			UpdateNuspecManifestContent(packageMetaData, arguments[1]);
			WriteNuspecToPackages(packageMetaData);
		}

		private static void WriteNuspecToPackages(Dictionary<string, NuspecContentEntry> packageMetaData)
		{
			foreach (var packageFile in packageMetaData.ToList())
			{
				var tempFile = Path.GetTempFileName();

				using (var original = ZipFile.Open(packageFile.Key, ZipArchiveMode.Read))
				using (var newArchive = ZipFile.Open(tempFile, ZipArchiveMode.Create))
				{
					foreach (var entry in original.Entries)
					{
						if (entry.FullName == packageFile.Value.EntryName)
						{
							// Remplace le .nuspec avec le contenu modifié
							var newEntry = newArchive.CreateEntry(entry.FullName);
							using (var writer = new StreamWriter(newEntry.Open(), Encoding.UTF8))
							{
								writer.Write(packageFile.Value.Contents);
							}
						}
						else
						{
							// Copie les autres fichiers tels quels
							var newEntry = newArchive.CreateEntry(entry.FullName, CompressionLevel.Optimal);
							using (var source = entry.Open())
							using (var dest = newEntry.Open())
							{
								source.CopyTo(dest);
							}
						}
					}
				}

				// Remplace l’archive originale par la nouvelle
				File.Delete(packageFile.Key);
				File.Move(tempFile, packageFile.Key);
			}
		}

		private static void UpdateNuspecManifestContent(Dictionary<string, NuspecContentEntry> packageMetaData, string dependencyNugetId)
		{
			foreach (var packageFile in packageMetaData.ToList())
			{
				var nuspecXmlDocument = new XmlDocument();
				nuspecXmlDocument.LoadXml(packageFile.Value.Contents);

				SetPackageDepencyVersionsToBeExplicitForXmlDocument(nuspecXmlDocument, dependencyNugetId);

				string updatedNuspecXml;
				using (var writer = new StringWriterWithEncoding(Encoding.UTF8))
				using (var xmlWriter = new XmlTextWriter(writer) { Formatting = Formatting.Indented })
				{
					nuspecXmlDocument.Save(xmlWriter);
					updatedNuspecXml = writer.ToString();
				}

				packageMetaData[packageFile.Key].Contents = updatedNuspecXml;
			}
		}

		private static void SetPackageDepencyVersionsToBeExplicitForXmlDocument(XmlDocument nuspecXmlDocument, string nugetIdFilter)
		{
			WalkDocumentNodes(nuspecXmlDocument.ChildNodes, node =>
			{
				if (node.Name.ToLowerInvariant() == "dependency" && !string.IsNullOrEmpty(node.Attributes["id"].Value) && node.Attributes["id"].Value.ToLowerInvariant().StartsWith(nugetIdFilter))
				{
					var currentVersion = node.Attributes["version"].Value;
					if (!node.Attributes["version"].Value.StartsWith("[") && !node.Attributes["version"].Value.EndsWith("]"))
					{
						node.Attributes["version"].Value = $"[{currentVersion}]";
					}
				}
			});
		}

		private static Dictionary<string, NuspecContentEntry> ReadNuspecFromPackages(DirectoryInfo packageDiscoverDirectoryInfo)
		{
			var packageNuspecDictionary = new Dictionary<string, NuspecContentEntry>();

			foreach (var packageFilePath in packageDiscoverDirectoryInfo.GetFiles("*.nupkg", SearchOption.AllDirectories))
			{
				using (var archive = ZipFile.OpenRead(packageFilePath.FullName))
				{
					foreach (var entry in archive.Entries)
					{
						if (entry.FullName.ToLowerInvariant().EndsWith(".nuspec"))
						{
							using (var reader = new StreamReader(entry.Open()))
							{
								var nuspecXml = reader.ReadToEnd();
								packageNuspecDictionary[packageFilePath.FullName] = new NuspecContentEntry
								{
									Contents = nuspecXml,
									EntryName = entry.FullName
								};
								break;
							}
						}
					}
				}
			}

			return packageNuspecDictionary;
		}

		private static void WalkDocumentNodes(XmlNodeList nodes, Action<XmlNode> callback)
		{
			foreach (XmlNode node in nodes)
			{
				callback(node);
				WalkDocumentNodes(node.ChildNodes, callback);
			}
		}
	}
}