using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace PackageResolver
{
    public static class PackageResolve
    {
        // Invoke with: -executeMethod PackageResolver.PackageResolve.Resolve
        public static void Resolve()
        {
            Debug.Log("[PackageResolve] Starting package resolution...");

            // Client.Resolve()는 동기적으로 패키지 의존성을 해결합니다
            Client.Resolve();
            
            Debug.Log("[PackageResolve] Package resolution completed.");
            EditorApplication.Exit(0);
        }
    }
}
