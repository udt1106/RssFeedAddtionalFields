using System.ServiceModel.Syndication;
using Sitecore.Diagnostics;
using Sitecore.Configuration;
using Sitecore.Data.Items;
using Sitecore.Pipelines;
using Sitecore.Pipelines.RenderField;
using Sitecore.Syndication;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;

//// This config file must be added into /App_Include/RssFeedAddtionalFields.config
//<configuration xmlns:patch="http://www.sitecore.net/xmlconfig/">
//    <sitecore>
//        <settings>
//            <!-- You can list new field names seperated by comma(,) -->
//            <setting name="RssFeedAddtionalFields.Fields.FieldNames" value="ThumbnailImage,Categories,Tags" />
//            <setting name="RssFeedAddtionalFields.Pipelines.RenderField" value="renderField" />
//        </settings>
//    </sitecore>
//</configuration>

namespace Sitecore.Custom.RSS
{
    public class NewRssFields : PublicFeed
    {
        private static List<string> FieldNames { get; set; }
        private static string RenderFieldPipelineName { get; set; }

        static NewRssFields()
        {
            FieldNames = Settings.GetSetting("RssFeedAddtionalFields.Fields.FieldNames").Split(',').ToList();
            RenderFieldPipelineName = Settings.GetSetting("RssFeedAddtionalFields.Pipelines.RenderField");
        }
 
        protected override SyndicationItem RenderItem(Item item)
        {
            SyndicationItem syndicationItem = base.RenderItem(item);
            AddHtmlToContent(syndicationItem, GetFieldHtmls(item));
            return syndicationItem;
        }

        protected virtual Dictionary<string, string> GetFieldHtmls(Item item)
        {
            if (FieldNames.Count <= 0)
            {
                return null;
            }
            return GetFieldHtmls(item, FieldNames);
        }

        private static Dictionary<string, string> GetFieldHtmls(Item item, List<string> FieldNames)
        {
            Assert.ArgumentNotNull(item, "item");
            Assert.ArgumentNotNullOrEmpty(RenderFieldPipelineName, "renderField");
            Dictionary<string, string> argsResult = new Dictionary<string, string>();

            foreach (var fieldName in FieldNames)
            {
                if (item == null)
                {
                    return argsResult;
                }
                RenderFieldArgs args = new RenderFieldArgs { Item = item, FieldName = fieldName };
                CorePipeline.Run(RenderFieldPipelineName, args);

                if (!args.Result.IsEmpty)
                {
                    string contents = args.Result.ToString();
                    string multilistItems = "";

                    // Image Field Type - Get only image path
                    if (args.FieldTypeKey == "image")
                    {
                        Sitecore.Data.Fields.ImageField iFld = args.Item.Fields[fieldName];
                        Sitecore.Resources.Media.MediaUrlOptions opt = new Sitecore.Resources.Media.MediaUrlOptions();
                        //opt.AlwaysIncludeServerUrl = true;
                        opt.AbsolutePath = true;
                        string mediaUrl = Sitecore.Resources.Media.MediaManager.GetMediaUrl(iFld.MediaItem, opt);
                        contents = mediaUrl;
                    }

                    // Multilist Field Type - Convert Item ID to Item Name
                    if (args.FieldTypeKey == "multilist")
                    {
                        List<string> itemId = args.Result.ToString().Split('|').ToList();
                        int count = 1;
                        foreach (string i in itemId)
                        {
                            Item theItem = item.Database.GetItem(Sitecore.Data.ID.Parse(i));
                            if (theItem != null)
                            {
                                multilistItems += theItem.Name;
                                if (count != itemId.Count)
                                {
                                    multilistItems += "|";
                                }
                            }
                            count++;
                        }
                        contents = multilistItems;
                    }
                    argsResult.Add(fieldName, contents);
                }
            }
            return argsResult;
        }

        protected virtual void AddHtmlToContent(SyndicationItem syndicationItem, Dictionary<string, string> getHtml)
        {
            if (!(syndicationItem.Content is TextSyndicationContent))
            {
                return;
            }
            foreach (KeyValuePair<string, string> pair in getHtml)
            {
                syndicationItem.ElementExtensions.Add(new XElement(pair.Key, new XCData(pair.Value)).CreateReader());
            }

            TextSyndicationContent content = syndicationItem.Content as TextSyndicationContent;
            syndicationItem.Content = new TextSyndicationContent("![CDATA[" + content.Text + "]]", TextSyndicationContentKind.Html);
        }
    }
}