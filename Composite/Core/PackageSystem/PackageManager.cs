﻿using System;
using System.Collections.Generic;
using Composite.Core.NewIO;
using System.Linq;
using System.Xml.Linq;
using Composite.Core.PackageSystem.Foundation;
using Composite.Core.Application;
using Composite.Core.Configuration;
using Composite.Core.IO;
using Composite.Core.ResourceSystem;
using Composite.C1Console.Security;
using Composite.Core.Xml;
using Composite.Core.Logging;


namespace Composite.Core.PackageSystem
{
    /// <summary>    
    /// </summary>
    /// <exclude />
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)] 
    public static class PackageManager
    {
        public static IEnumerable<InstalledPackageInformation> GetInstalledPackages()
        {
            string baseDirectory = PathUtil.Resolve(GlobalSettingsFacade.PackageDirectory);

            if (Directory.Exists(baseDirectory) == false) yield break;

            string[] packageDirectories = Directory.GetDirectories(baseDirectory);
            foreach (string packageDirecoty in packageDirectories)
            {
                if (File.Exists(System.IO.Path.Combine(packageDirecoty, PackageSystemSettings.InstalledFilename)) == true)
                {
                    string filename = System.IO.Path.Combine(packageDirecoty, PackageSystemSettings.PackageInformationFilename);

                    if (File.Exists(filename) == true)
                    {
                        XDocument doc = XDocumentUtils.Load(filename);

                        XElement packageInfoElement = doc.Root;
                        if (packageInfoElement.Name != XmlUtils.GetXName(PackageSystemSettings.XmlNamespace, PackageSystemSettings.PackageInfoElementName)) throw new InvalidOperationException(string.Format("{0} is wrongly formattet", filename));

                        XAttribute nameAttribute = GetAttributeNotNull(filename, packageInfoElement, PackageSystemSettings.PackageInfo_NameAttributeName);
                        XAttribute groupNameAttribute = GetAttributeNotNull(filename, packageInfoElement, PackageSystemSettings.PackageInfo_GroupNameAttributeName);
                        XAttribute versionAttribute = GetAttributeNotNull(filename, packageInfoElement, PackageSystemSettings.PackageInfo_VersionAttributeName);
                        XAttribute authorAttribute = GetAttributeNotNull(filename, packageInfoElement, PackageSystemSettings.PackageInfo_AuthorAttributeName);
                        XAttribute websiteAttribute = GetAttributeNotNull(filename, packageInfoElement, PackageSystemSettings.PackageInfo_WebsiteAttributeName);
                        XAttribute descriptionAttribute = GetAttributeNotNull(filename, packageInfoElement, PackageSystemSettings.PackageInfo_DescriptionAttributeName);
                        XAttribute installDateAttribute = GetAttributeNotNull(filename, packageInfoElement, PackageSystemSettings.PackageInfo_InstallDateAttributeName);
                        XAttribute installedByAttribute = GetAttributeNotNull(filename, packageInfoElement, PackageSystemSettings.PackageInfo_InstalledByAttributeName);
                        XAttribute isLocalInstalledAttribute = GetAttributeNotNull(filename, packageInfoElement, PackageSystemSettings.PackageInfo_IsLocalInstalledAttributeName);
                        XAttribute canBeUninstalledAttribute = GetAttributeNotNull(filename, packageInfoElement, PackageSystemSettings.PackageInfo_CanBeUninstalledAttributeName);
                        XAttribute flushOnCompletionAttribute = GetAttributeNotNull(filename, packageInfoElement, PackageSystemSettings.PackageInfo_FlushOnCompletionAttributeName);
                        XAttribute reloadConsoleOnCompletionAttribute = GetAttributeNotNull(filename, packageInfoElement, PackageSystemSettings.PackageInfo_ReloadConsoleOnCompletionAttributeName);
                        XAttribute systemLockingAttribute = GetAttributeNotNull(filename, packageInfoElement, PackageSystemSettings.PackageInfo_SystemLockingAttributeName);

                        XAttribute packageServerAddressAttribute = packageInfoElement.Attribute(PackageSystemSettings.PackageInfo_PackageServerAddressAttributeName);


                        SystemLockingType systemLockingType;
                        if (systemLockingAttribute.TryDeserialize(out systemLockingType) == false) throw new InvalidOperationException("The systemLocking attibute value is wrong");

                        string path = packageDirecoty.Remove(0, baseDirectory.Length);
                        if (path.StartsWith("\\") == true)
                        {
                            path = path.Remove(0, 1);
                        }

                        yield return new InstalledPackageInformation
                        {
                            Id = new Guid(path),
                            Name = nameAttribute.Value,
                            GroupName = groupNameAttribute.Value,
                            Version = versionAttribute.Value,
                            Author = authorAttribute.Value,
                            Website = websiteAttribute.Value,
                            Description = descriptionAttribute.Value,
                            InstallDate = (DateTime)installDateAttribute,
                            InstalledBy = installedByAttribute.Value,
                            IsLocalInstalled = (bool)isLocalInstalledAttribute,
                            CanBeUninstalled = (bool)canBeUninstalledAttribute,
                            FlushOnCompletion = (bool)flushOnCompletionAttribute,
                            ReloadConsoleOnCompletion = (bool)reloadConsoleOnCompletionAttribute,
                            SystemLockingType = systemLockingType,
                            PackageServerAddress = packageServerAddressAttribute != null ? packageServerAddressAttribute.Value : null,
                            PackageInstallPath = packageDirecoty
                        };
                    }
                    else
                    {
                        throw new InvalidOperationException(string.Format("{0} does not exist", filename));
                    }
                }
                else
                {
                    // Make this cleanup in an other way, it works correctly if it is done between validation and installation.
                    //LoggingService.LogVerbose("PackageManager", string.Format("Uncomlete installed add on found ('{0}'), deleting it", System.IO.Path.GetFileName(packageDirecoty)));
                    //try
                    //{
                    //    Directory.Delete(packageDirecoty, true);
                    //}
                    //catch (Exception)
                    //{
                    //}
                }
            }
        }


        private static XAttribute GetAttributeNotNull(string fileName, XElement packageInfoElement,  string attributeName)
        {
            XAttribute attribute = packageInfoElement.Attribute(attributeName);
            Verify.IsNotNull(attribute, "File: '{0}', failed to find '{1}' attribute.", fileName, attributeName);

            return attribute;
        }


        public static bool IsInstalled(Guid packageId)
        {
            InstalledPackageInformation installedPackageInformation =
                        (from ao in GetInstalledPackages()
                         where ao.Id == packageId
                         select ao).SingleOrDefault();

            return installedPackageInformation != null;
        }



        public static string GetCurrentVersion(Guid packageId)
        {
            string currentVersion =
                        (from ao in GetInstalledPackages()
                         where ao.Id == packageId
                         select ao.Version).SingleOrDefault();

            return currentVersion;
        }



        public static PackageManagerInstallProcess Install(System.IO.Stream zipFileStream, bool isLocalInstall)
        {
            if (isLocalInstall == false) throw new ArgumentException("Non local install needs a packageServerAddress");

            return Install(zipFileStream, isLocalInstall, null);
        }



        public static PackageManagerInstallProcess Install(System.IO.Stream zipFileStream, bool isLocalInstall, string packageServerAddress)
        {
            if ((isLocalInstall == false) && (string.IsNullOrEmpty(packageServerAddress) == true)) throw new ArgumentException("Non local install needs a packageServerAddress");

            string zipFilename = null;

            PackageFragmentValidationResult packageFragmentValidationResult;
            try
            {                
                packageFragmentValidationResult = SaveZipFile(zipFileStream, out zipFilename);
                if (packageFragmentValidationResult != null) return new PackageManagerInstallProcess(new List<PackageFragmentValidationResult> { packageFragmentValidationResult }, null);

                XElement installContent;
                packageFragmentValidationResult = XmlHelper.LoadInstallXml(zipFilename, out installContent);
                if (packageFragmentValidationResult != null) return new PackageManagerInstallProcess(new List<PackageFragmentValidationResult> { packageFragmentValidationResult }, zipFilename);

                PackageInformation packageInformation;
                packageFragmentValidationResult = ValidatePackageInformation(installContent, out packageInformation);
                if (packageFragmentValidationResult != null) return new PackageManagerInstallProcess(new List<PackageFragmentValidationResult> { packageFragmentValidationResult }, zipFilename);                

                if ((RuntimeInformation.ProductVersion < packageInformation.MinCompositeVersionSupported) ||
                    (RuntimeInformation.ProductVersion > packageInformation.MaxCompositeVersionSupported)) return new PackageManagerInstallProcess(new List<PackageFragmentValidationResult> { new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.CompositeVersionMisMatch")) }, zipFilename);

                if (IsInstalled(packageInformation.Id) == true)
                {
                    string currentVersionString = GetCurrentVersion(packageInformation.Id);

                    Version currentVersion = new Version(currentVersionString);
                    Version newVersion = new Version(packageInformation.Version);

                    if (newVersion <= currentVersion)
                    {
                        return new PackageManagerInstallProcess(new List<PackageFragmentValidationResult> { new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.NewerVersionInstlled")) }, zipFilename);
                    }
                }

                string packageInstallDirectory = CreatePackageDirectoryName(packageInformation);
                Directory.CreateDirectory(packageInstallDirectory);

                string packageZipFilename = System.IO.Path.Combine(packageInstallDirectory, System.IO.Path.GetFileName(zipFilename));
                File.Copy(zipFilename, packageZipFilename, true);

                string username = "Composite";
                if (UserValidationFacade.IsLoggedIn() == true)
                {
                    username = UserValidationFacade.GetUsername();
                }

                XDocument doc = new XDocument();
                XElement packageInfoElement = new XElement(XmlUtils.GetXName(PackageSystemSettings.XmlNamespace, PackageSystemSettings.PackageInfoElementName));
                doc.Add(packageInfoElement);
                packageInfoElement.Add(new XAttribute(PackageSystemSettings.PackageInfo_NameAttributeName, packageInformation.Name));
                packageInfoElement.Add(new XAttribute(PackageSystemSettings.PackageInfo_GroupNameAttributeName, packageInformation.GroupName));
                packageInfoElement.Add(new XAttribute(PackageSystemSettings.PackageInfo_VersionAttributeName, packageInformation.Version));
                packageInfoElement.Add(new XAttribute(PackageSystemSettings.PackageInfo_AuthorAttributeName, packageInformation.Author));
                packageInfoElement.Add(new XAttribute(PackageSystemSettings.PackageInfo_WebsiteAttributeName, packageInformation.Website));
                packageInfoElement.Add(new XAttribute(PackageSystemSettings.PackageInfo_DescriptionAttributeName, packageInformation.Description));
                packageInfoElement.Add(new XAttribute(PackageSystemSettings.PackageInfo_InstallDateAttributeName, DateTime.Now));
                packageInfoElement.Add(new XAttribute(PackageSystemSettings.PackageInfo_InstalledByAttributeName, username));
                packageInfoElement.Add(new XAttribute(PackageSystemSettings.PackageInfo_IsLocalInstalledAttributeName, isLocalInstall));
                packageInfoElement.Add(new XAttribute(PackageSystemSettings.PackageInfo_CanBeUninstalledAttributeName, packageInformation.CanBeUninstalled));
                packageInfoElement.Add(new XAttribute(PackageSystemSettings.PackageInfo_FlushOnCompletionAttributeName, packageInformation.FlushOnCompletion));
                packageInfoElement.Add(new XAttribute(PackageSystemSettings.PackageInfo_ReloadConsoleOnCompletionAttributeName, packageInformation.ReloadConsoleOnCompletion));
                packageInfoElement.Add(new XAttribute(PackageSystemSettings.PackageInfo_SystemLockingAttributeName, packageInformation.SystemLockingType.Serialize()));
                if (string.IsNullOrEmpty(packageServerAddress) == false)
                {
                    packageInfoElement.Add(new XAttribute(PackageSystemSettings.PackageInfo_PackageServerAddressAttributeName, packageServerAddress));
                }

                string infoFilename = System.IO.Path.Combine(packageInstallDirectory, PackageSystemSettings.PackageInformationFilename);              
                XDocumentUtils.Save(doc, infoFilename);

                PackageInstaller packageInstaller = new PackageInstaller(new PackageInstallerUninstallerFactory(), packageZipFilename, packageInstallDirectory, TempDirectoryFacade.CreateTempDirectory(), packageInformation);

                PackageManagerInstallProcess packageManagerInstallProcess = new PackageManagerInstallProcess(packageInstaller, packageInformation.SystemLockingType, zipFilename, packageInstallDirectory, packageInformation.Name, packageInformation.Id);
                return packageManagerInstallProcess;
            }
            catch (Exception ex)
            {
                return new PackageManagerInstallProcess(new List<PackageFragmentValidationResult> { new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, ex) }, zipFilename);
            }
        }



        public static PackageManagerUninstallProcess Uninstall(Guid id)
        {
            try
            {
                string absolutePath = System.IO.Path.Combine(PathUtil.Resolve(GlobalSettingsFacade.PackageDirectory), id.ToString());

                InstalledPackageInformation installedPackageInformation =
                    (from addon in GetInstalledPackages()
                     where addon.Id == id
                     select addon).SingleOrDefault();

                if (installedPackageInformation == null) return new PackageManagerUninstallProcess(new List<PackageFragmentValidationResult> { new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, string.Format(StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.MissingAddOnDirectory"), absolutePath)) });

                LoggingService.LogVerbose("PackageManager", string.Format("Uninstalling package: {0}, Id = {1}", installedPackageInformation.Name, installedPackageInformation.Id));

                if (installedPackageInformation.CanBeUninstalled == false) return new PackageManagerUninstallProcess(new List<PackageFragmentValidationResult> { new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.Uninstallable")) });

                string zipFilePath = System.IO.Path.Combine(absolutePath, PackageSystemSettings.ZipFilename);
                if (File.Exists(zipFilePath) == false) return new PackageManagerUninstallProcess(new List<PackageFragmentValidationResult> { new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, string.Format(StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.MissingZipFile"), zipFilePath)) });

                string uninstallFilePath = System.IO.Path.Combine(absolutePath, PackageSystemSettings.UninstallFilename);
                if (File.Exists(uninstallFilePath) == false) return new PackageManagerUninstallProcess(new List<PackageFragmentValidationResult> { new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, string.Format(StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.MissingUninstallFile"), uninstallFilePath)) });

                PackageUninstaller packageUninstaller = new PackageUninstaller(zipFilePath, uninstallFilePath, absolutePath, TempDirectoryFacade.CreateTempDirectory(), installedPackageInformation.FlushOnCompletion, installedPackageInformation.ReloadConsoleOnCompletion, true);

                PackageManagerUninstallProcess packageManagerUninstallProcess = new PackageManagerUninstallProcess(packageUninstaller, absolutePath, installedPackageInformation.SystemLockingType);
                return packageManagerUninstallProcess;
            }
            catch (Exception ex)
            {
                return new PackageManagerUninstallProcess(new List<PackageFragmentValidationResult> { new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, ex) });
            }
        }



        internal static PackageFragmentValidationResult ValidatePackageInformation(XElement installContent, out PackageInformation packageInformation)
        {
            packageInformation = null;

            XElement packageInformationElement = installContent.Element(XmlUtils.GetXName(PackageSystemSettings.XmlNamespace, PackageSystemSettings.PackageInformationElementName));
            if (packageInformationElement == null) return new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, string.Format(StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.MissingElement"), PackageSystemSettings.PackageInformationElementName), installContent);

            XAttribute idAttribute = packageInformationElement.Attribute(PackageSystemSettings.IdAttributeName);
            XAttribute nameAttribute = packageInformationElement.Attribute(PackageSystemSettings.NameAttributeName);
            XAttribute groupNameAttribute = packageInformationElement.Attribute(PackageSystemSettings.GroupNameAttributeName);
            XAttribute authorAttribute = packageInformationElement.Attribute(PackageSystemSettings.AuthorAttributeName);
            XAttribute websiteAttribute = packageInformationElement.Attribute(PackageSystemSettings.WebsiteAttributeName);
            XAttribute versionAttribute = packageInformationElement.Attribute(PackageSystemSettings.VersionAttributeName);
            XAttribute canBeUninstalledAttribute = packageInformationElement.Attribute(PackageSystemSettings.CanBeUninstalledAttributeName);
            XAttribute systemLockingAttribute = packageInformationElement.Attribute(PackageSystemSettings.SystemLockingAttributeName);
            XAttribute flushOnCompletionAttribute = packageInformationElement.Attribute(PackageSystemSettings.FlushOnCompletionAttributeName);
            XAttribute reloadConsoleOnCompletionAttribute = packageInformationElement.Attribute(PackageSystemSettings.ReloadConsoleOnCompletionAttributeName);

            if (idAttribute == null) return new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, string.Format(StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.MissingAttribute"), PackageSystemSettings.IdAttributeName), packageInformationElement);
            if (nameAttribute == null) return new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, string.Format(StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.MissingAttribute"), PackageSystemSettings.NameAttributeName), packageInformationElement);
            if (groupNameAttribute == null) return new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, string.Format(StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.MissingAttribute"), PackageSystemSettings.GroupNameAttributeName), packageInformationElement);
            if (authorAttribute == null) return new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, string.Format(StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.MissingAttribute"), PackageSystemSettings.AuthorAttributeName), packageInformationElement);
            if (websiteAttribute == null) return new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, string.Format(StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.MissingAttribute"), PackageSystemSettings.WebsiteAttributeName), packageInformationElement);
            if (versionAttribute == null) return new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, string.Format(StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.MissingAttribute"), PackageSystemSettings.VersionAttributeName), packageInformationElement);
            if (canBeUninstalledAttribute == null) return new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, string.Format(StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.MissingAttribute"), PackageSystemSettings.CanBeUninstalledAttributeName), packageInformationElement);
            if (systemLockingAttribute == null) return new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, string.Format(StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.MissingAttribute"), PackageSystemSettings.SystemLockingAttributeName), packageInformationElement);

            if (string.IsNullOrEmpty(nameAttribute.Value) == true) return new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, string.Format(StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.InvalidAttributeValue"), PackageSystemSettings.NameAttributeName), nameAttribute);
            if (string.IsNullOrEmpty(groupNameAttribute.Value) == true) return new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, string.Format(StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.InvalidAttributeValue"), PackageSystemSettings.GroupNameAttributeName), groupNameAttribute);
            if (string.IsNullOrEmpty(authorAttribute.Value) == true) return new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, string.Format(StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.InvalidAttributeValue"), PackageSystemSettings.AuthorAttributeName), authorAttribute);
            if (string.IsNullOrEmpty(websiteAttribute.Value) == true) return new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, string.Format(StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.InvalidAttributeValue"), PackageSystemSettings.WebsiteAttributeName), websiteAttribute);
            if (string.IsNullOrEmpty(versionAttribute.Value) == true) return new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, string.Format(StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.InvalidAttributeValue"), PackageSystemSettings.VersionAttributeName), versionAttribute);
            if (string.IsNullOrEmpty(packageInformationElement.Value) == true) return new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, string.Format(StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.InvalidElementValue"), PackageSystemSettings.PackageInformationElementName), packageInformationElement);

            Guid id;
            if (idAttribute.TryGetGuidValue(out id) == false) return new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, string.Format(StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.InvalidAttributeValue"), PackageSystemSettings.IdAttributeName), idAttribute);


            string newVersion;

            if (VersionStringHelper.ValidateVersion(versionAttribute.Value, out newVersion) == true) versionAttribute.Value = newVersion;
            else return new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, string.Format(StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.InvalidAttributeValue"), PackageSystemSettings.VersionAttributeName), versionAttribute);

            bool canBeUninstalled;
            if (canBeUninstalledAttribute.TryGetBoolValue(out canBeUninstalled) == false) return new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, string.Format(StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.InvalidAttributeValue"), PackageSystemSettings.CanBeUninstalledAttributeName), canBeUninstalledAttribute);

            SystemLockingType systemLockingType;
            if (systemLockingAttribute.TryDeserialize(out systemLockingType) == false) return new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, string.Format(StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.InvalidAttributeValue"), PackageSystemSettings.SystemLockingAttributeName), systemLockingAttribute);

            bool flushOnCompletion = false;
            if ((flushOnCompletionAttribute != null) && (flushOnCompletionAttribute.TryGetBoolValue(out flushOnCompletion) == false)) return new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, string.Format(StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.InvalidAttributeValue"), PackageSystemSettings.FlushOnCompletionAttributeName), flushOnCompletionAttribute);

            bool reloadConsoleOnCompletion = false;
            if ((reloadConsoleOnCompletionAttribute != null) && (reloadConsoleOnCompletionAttribute.TryGetBoolValue(out reloadConsoleOnCompletion) == false)) return new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, string.Format(StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.InvalidAttributeValue"), PackageSystemSettings.ReloadConsoleOnCompletionAttributeName), reloadConsoleOnCompletionAttribute);


            XElement packageRequirementsElement = installContent.Element(XmlUtils.GetXName(PackageSystemSettings.XmlNamespace, PackageSystemSettings.PackageRequirementsElementName));
            if (packageRequirementsElement == null) return new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, string.Format(StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.MissingElement"), PackageSystemSettings.PackageRequirementsElementName), installContent);

            XAttribute minimumCompositeVersionAttribute = packageRequirementsElement.Attribute(PackageSystemSettings.MinimumCompositeVersionAttributeName);
            XAttribute maximumCompositeVersionAttribute = packageRequirementsElement.Attribute(PackageSystemSettings.MaximumCompositeVersionAttributeName);

            if (minimumCompositeVersionAttribute == null) return new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, string.Format(StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.MissingAttribute"), PackageSystemSettings.MinimumCompositeVersionAttributeName), packageRequirementsElement);
            if (maximumCompositeVersionAttribute == null) return new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, string.Format(StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.MissingAttribute"), PackageSystemSettings.MaximumCompositeVersionAttributeName), packageRequirementsElement);

            if (string.IsNullOrEmpty(minimumCompositeVersionAttribute.Value) == true) return new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, string.Format(StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.InvalidAttributeValue"), PackageSystemSettings.MinimumCompositeVersionAttributeName), minimumCompositeVersionAttribute);
            if (string.IsNullOrEmpty(maximumCompositeVersionAttribute.Value) == true) return new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, string.Format(StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.InvalidAttributeValue"), PackageSystemSettings.MaximumCompositeVersionAttributeName), maximumCompositeVersionAttribute);

            if (VersionStringHelper.ValidateVersion(minimumCompositeVersionAttribute.Value, out newVersion) == true) minimumCompositeVersionAttribute.Value = newVersion;
            else return new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, string.Format(StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.InvalidAttributeValue"), PackageSystemSettings.VersionAttributeName), minimumCompositeVersionAttribute);
            
            if (VersionStringHelper.ValidateVersion(maximumCompositeVersionAttribute.Value, out newVersion) == true) maximumCompositeVersionAttribute.Value = newVersion;
            else return new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, string.Format(StringResourceSystemFacade.GetString("Composite.Core.PackageSystem.PackageFragmentInstallers", "AddOnManager.InvalidAttributeValue"), PackageSystemSettings.VersionAttributeName), maximumCompositeVersionAttribute);


            packageInformation = new PackageInformation();
            packageInformation.Id = id;
            packageInformation.Name = nameAttribute.Value;
            packageInformation.GroupName = groupNameAttribute.Value;
            packageInformation.Author = authorAttribute.Value;
            packageInformation.Website = websiteAttribute.Value;
            packageInformation.Version = versionAttribute.Value;
            packageInformation.CanBeUninstalled = canBeUninstalled;
            packageInformation.SystemLockingType = systemLockingType;
            packageInformation.Description = packageInformationElement.Value;
            packageInformation.FlushOnCompletion = flushOnCompletion;
            packageInformation.ReloadConsoleOnCompletion = reloadConsoleOnCompletion;
            packageInformation.MinCompositeVersionSupported = new Version(minimumCompositeVersionAttribute.Value);
            packageInformation.MaxCompositeVersionSupported = new Version(maximumCompositeVersionAttribute.Value);

            return null;
        }



        private static PackageFragmentValidationResult SaveZipFile(System.IO.Stream zipFileStream, out string zipFilename)
        {
            zipFilename = null;

            try
            {
                zipFilename = System.IO.Path.Combine(PathUtil.Resolve(GlobalSettingsFacade.PackageDirectory), PackageSystemSettings.ZipFilename);

                if (File.Exists(zipFilename) == true)
                {
                    File.Delete(zipFilename);
                }

                if (Directory.Exists(System.IO.Path.GetDirectoryName(zipFilename)) == false)
                {
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(zipFilename));
                }

                using (System.IO.Stream readStream = zipFileStream)
                {
                    using (FileStream fileStream = new FileStream(zipFilename, System.IO.FileMode.Create))
                    {
                        byte[] buffer = new byte[4096];

                        int readBytes;
                        while ((readBytes = readStream.Read(buffer, 0, 4096)) > 0)
                        {
                            fileStream.Write(buffer, 0, readBytes);
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                return new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, ex);
            }
        }



        private static string CreatePackageDirectoryName(PackageInformation packageInformation)
        {
            string direcotryName = string.Format("{0}", packageInformation.Id);

            return System.IO.Path.Combine(PathUtil.Resolve(GlobalSettingsFacade.PackageDirectory), direcotryName);
        }
    }
}
