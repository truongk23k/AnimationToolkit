using System.IO;
using UnityEditor;
using UnityEngine;

namespace AnimationToolkit.EditorTools
{
    /// <summary>
    /// Cài/cập nhật skill cho Claude Code: chép Assets/AnimationToolkit/AI-Skill sang .claude/skills/unity-animation-toolkit
    /// của project. Nguồn duy nhất vẫn là thư mục trong package; bản trong .claude chỉ là bản sao để Claude Code tìm thấy.
    /// </summary>
    public static class SkillInstaller
    {

        public static void Install()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string src = Path.Combine(Application.dataPath, "AnimationToolkit", "AI-Skill");
            string dst = Path.Combine(projectRoot, ".claude", "skills", "unity-animation-toolkit");
            if (!Directory.Exists(src)) { Debug.LogError("AI-Skill folder not found: " + src); return; }
            if (Directory.Exists(dst)) Directory.Delete(dst, true);
            CopyDir(src, dst);
            Debug.Log("Animation Toolkit: skill installed to " + dst);
        }

        static void CopyDir(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (string f in Directory.GetFiles(src))
            {
                if (f.EndsWith(".meta")) continue; // meta của Unity không cần cho skill
                File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
            }
            foreach (string d in Directory.GetDirectories(src))
                CopyDir(d, Path.Combine(dst, Path.GetFileName(d)));
        }
    }
}
