#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

namespace YeonaMathAdventure.Editor
{
    /// <summary>
    /// Configures the Android SDK, NDK and JDK used by Unity.
    ///
    /// Environment variable priority:
    ///   SDK: YEONA_ANDROID_SDK_ROOT, ANDROID_SDK_ROOT, ANDROID_HOME
    ///   NDK: YEONA_ANDROID_NDK_ROOT, ANDROID_NDK_ROOT
    ///   JDK: YEONA_ANDROID_JDK_ROOT, JAVA_HOME
    ///
    /// If an environment value is absent, the helper falls back to a valid
    /// Unity setting and then to conventional Windows/embedded locations.
    /// </summary>
    public static class YeonaAndroidExternalTools
    {
        private const string PreferredNdkVersion = "27.2.12479018";

        [MenuItem("Yeona Math/Android/Configure External Tools")]
        public static void ConfigureFromEnvironment()
        {
            string sdkRoot = ResolveSdkRoot();
            string ndkRoot = ResolveNdkRoot(sdkRoot);
            string jdkRoot = ResolveJdkRoot();

            ValidateSdk(sdkRoot);
            ValidateNdk(ndkRoot);
            ValidateJdk(jdkRoot);

            AndroidExternalToolsSettings.sdkRootPath = sdkRoot;
            AndroidExternalToolsSettings.ndkRootPath = ndkRoot;
            AndroidExternalToolsSettings.jdkRootPath = jdkRoot;
            AndroidExternalToolsSettings.Gradle.stopDaemonsOnExit = true;

            string gradleUserHome = FirstNonEmpty(
                Environment.GetEnvironmentVariable("YEONA_GRADLE_USER_HOME"),
                Environment.GetEnvironmentVariable("GRADLE_USER_HOME"));
            if (!string.IsNullOrWhiteSpace(gradleUserHome))
            {
                gradleUserHome = NormalizePath(gradleUserHome);
                Directory.CreateDirectory(gradleUserHome);
                AndroidExternalToolsSettings.Gradle.userHomePath = gradleUserHome;
                Environment.SetEnvironmentVariable("GRADLE_USER_HOME", gradleUserHome);
            }

            // Keep Gradle child processes and any build hooks on the same toolchain.
            Environment.SetEnvironmentVariable("ANDROID_SDK_ROOT", sdkRoot);
            Environment.SetEnvironmentVariable("ANDROID_HOME", sdkRoot);
            Environment.SetEnvironmentVariable("ANDROID_NDK_ROOT", ndkRoot);
            Environment.SetEnvironmentVariable("JAVA_HOME", jdkRoot);

            AssetDatabase.SaveAssets();
            Debug.Log(
                "[Yeona Android Tools] Configured external tools.\n" +
                $"SDK: {sdkRoot}\n" +
                $"NDK: {ndkRoot}\n" +
                $"JDK: {jdkRoot}\n" +
                $"Gradle user home: {AndroidExternalToolsSettings.Gradle.userHomePath}");
        }

        [MenuItem("Yeona Math/Android/Print External Tools")]
        public static void PrintCurrent()
        {
            Debug.Log(
                "[Yeona Android Tools] Current external tools.\n" +
                $"SDK: {AndroidExternalToolsSettings.sdkRootPath}\n" +
                $"NDK: {AndroidExternalToolsSettings.ndkRootPath}\n" +
                $"JDK: {AndroidExternalToolsSettings.jdkRootPath}\n" +
                $"Gradle: {AndroidExternalToolsSettings.Gradle.path}\n" +
                $"Gradle user home: {AndroidExternalToolsSettings.Gradle.userHomePath}");
        }

        private static string ResolveSdkRoot()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string conventionalSdk = string.IsNullOrWhiteSpace(localAppData)
                ? null
                : Path.Combine(localAppData, "Android", "Sdk");

            return FirstValidDirectory(
                Environment.GetEnvironmentVariable("YEONA_ANDROID_SDK_ROOT"),
                Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT"),
                Environment.GetEnvironmentVariable("ANDROID_HOME"),
                AndroidExternalToolsSettings.sdkRootPath,
                conventionalSdk);
        }

        private static string ResolveNdkRoot(string sdkRoot)
        {
            string explicitRoot = FirstValidDirectory(
                Environment.GetEnvironmentVariable("YEONA_ANDROID_NDK_ROOT"),
                Environment.GetEnvironmentVariable("ANDROID_NDK_ROOT"),
                Path.Combine(sdkRoot, "ndk", PreferredNdkVersion),
                AndroidExternalToolsSettings.ndkRootPath);
            if (!string.IsNullOrWhiteSpace(explicitRoot))
            {
                return explicitRoot;
            }

            string ndkParent = Path.Combine(sdkRoot, "ndk");
            if (Directory.Exists(ndkParent))
            {
                string candidate = Directory.GetDirectories(ndkParent)
                    .Where(IsValidNdkDirectory)
                    .OrderByDescending(path => ParseVersion(Path.GetFileName(path)))
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return NormalizePath(candidate);
                }
            }

            return null;
        }

        private static string ResolveJdkRoot()
        {
            string editorDirectory = Path.GetDirectoryName(EditorApplication.applicationPath);
            string embeddedJdk = string.IsNullOrWhiteSpace(editorDirectory)
                ? null
                : Path.Combine(editorDirectory, "Data", "PlaybackEngines", "AndroidPlayer", "OpenJDK");

            return FirstValidDirectory(
                Environment.GetEnvironmentVariable("YEONA_ANDROID_JDK_ROOT"),
                Environment.GetEnvironmentVariable("JAVA_HOME"),
                AndroidExternalToolsSettings.jdkRootPath,
                embeddedJdk);
        }

        private static void ValidateSdk(string root)
        {
            RequireRoot(root, "Android SDK");
            RequireDirectory(Path.Combine(root, "platforms"), "Android SDK platforms");
            RequireDirectory(Path.Combine(root, "build-tools"), "Android SDK build-tools");
            RequireDirectory(Path.Combine(root, "platform-tools"), "Android SDK platform-tools");
        }

        private static void ValidateNdk(string root)
        {
            RequireRoot(root, "Android NDK");
            RequireFile(Path.Combine(root, "source.properties"), "Android NDK source.properties");
            RequireDirectory(Path.Combine(root, "toolchains", "llvm"), "Android NDK LLVM toolchain");
        }

        private static void ValidateJdk(string root)
        {
            RequireRoot(root, "Java Development Kit");
            string executable = Application.platform == RuntimePlatform.WindowsEditor ? "java.exe" : "java";
            RequireFile(Path.Combine(root, "bin", executable), "JDK java executable");
        }

        private static bool IsValidNdkDirectory(string path)
        {
            return Directory.Exists(path)
                   && File.Exists(Path.Combine(path, "source.properties"))
                   && Directory.Exists(Path.Combine(path, "toolchains", "llvm"));
        }

        private static Version ParseVersion(string value)
        {
            return Version.TryParse(value, out Version parsed) ? parsed : new Version(0, 0);
        }

        private static string FirstValidDirectory(params string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                string normalized = NormalizePath(candidate);
                if (Directory.Exists(normalized))
                {
                    return normalized;
                }
            }

            return null;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }

        private static string NormalizePath(string path)
        {
            string unquoted = path.Trim().Trim('"');
            return Path.GetFullPath(unquoted)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static void RequireRoot(string path, string description)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                throw new DirectoryNotFoundException(
                    $"{description} was not found. Set the corresponding YEONA_ANDROID_*_ROOT environment variable. Resolved path: '{path ?? "<null>"}'.");
            }
        }

        private static void RequireDirectory(string path, string description)
        {
            if (!Directory.Exists(path))
            {
                throw new DirectoryNotFoundException($"{description} was not found at '{path}'.");
            }
        }

        private static void RequireFile(string path, string description)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"{description} was not found at '{path}'.", path);
            }
        }
    }
}
#endif
