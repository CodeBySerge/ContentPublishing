using System.Web.Optimization;

namespace ContentPublishing.Web
{
    public class BundleConfig
    {
        public static void RegisterBundles(BundleCollection bundles)
        {
            // Script bundles are intentionally disabled in this scaffold because
            // the Scripts directory is not included in the repository yet.

            BundleTable.EnableOptimizations = false;
        }
    }
}
