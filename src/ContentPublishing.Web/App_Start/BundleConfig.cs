using System.Web.Optimization;

namespace ContentPublishing.Web
{
    public class BundleConfig
    {
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.Add(new ScriptBundle("~/bundles/jquery").Include("~/Scripts/jquery-{version}.js"));
            bundles.Add(new ScriptBundle("~/bundles/jqueryval")
                .Include("~/Scripts/jquery.validate*", "~/Scripts/jquery.validate.unobtrusive*"));

            BundleTable.EnableOptimizations = false;
        }
    }
}
