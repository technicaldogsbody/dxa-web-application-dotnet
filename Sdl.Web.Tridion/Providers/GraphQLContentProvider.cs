﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using Sdl.Web.Common;
using Sdl.Web.Common.Interfaces;
using Sdl.Web.Common.Logging;
using Sdl.Web.Common.Models;
using Sdl.Web.DataModel;
using Sdl.Web.Tridion.Providers.Query;
using Sdl.Web.Tridion.Statics;
using Tridion.ContentDelivery.DynamicContent;
using Tridion.ContentDelivery.DynamicContent.Query;
using Tridion.ContentDelivery.Meta;

namespace Sdl.Web.Tridion.Mapping
{
    /// <summary>
    /// GraphQL Content Provider implementation (based on Public Content Api).
    /// </summary>
    public class GraphQLContentProvider : IContentProvider, IRawDataProvider
    {
        #region Cursor
        internal class CursorMap : Dictionary<int, string>
        {
            private const string SessionKey = "dxa_cursors";
          
            private static CursorMap GetCursorMap(string id)
            {              
                var cursors = (Dictionary<string, CursorMap>)HttpContext.Current.Session[SessionKey] ?? new Dictionary<string, CursorMap>();
                if (!cursors.ContainsKey(id))
                {
                    cursors.Add(id, new CursorMap());                  
                }
                HttpContext.Current.Session[SessionKey] = cursors;
                return cursors[id];
            }
          
            public static string GetCursor(string id, ref int start)
            {
                if (start == 0) return null;

                CursorMap cursorMap = GetCursorMap(id);
                
                if(cursorMap.ContainsKey(start)) return cursorMap[start];

                if (cursorMap.Count == 0)
                {
                    start = 0;
                    return null;
                }

                int min = 0;
                foreach (var x in cursorMap.Keys)
                {
                    if (x >= min && x < start)
                        min = x;
                }
                start = min;
                return start == 0 ? null : cursorMap[start];
            }

            public static void SetCursor(string id, int start, string cursor)
            {
                var cursorMap = GetCursorMap(id);
                cursorMap[start] = cursor;
            }
        }
        #endregion

        private readonly IModelService _modelService;

        public GraphQLContentProvider()
        {
            _modelService = new Providers.ModelService.ModelService();
            ModelBuilderPipeline.Init();
        }

        /// <summary>
        /// Gets a Page Model for a given URL path.
        /// </summary>
        /// <param name="urlPath">The URL path (unescaped).</param>
        /// <param name="localization">The context <see cref="ILocalization"/>.</param>
        /// <param name="addIncludes">Indicates whether include Pages should be expanded.</param>
        /// <returns>The Page Model.</returns>
        /// <exception cref="DxaItemNotFoundException">If no Page Model exists for the given URL.</exception>
        public virtual PageModel GetPageModel(string urlPath, ILocalization localization, bool addIncludes = true)
            => _modelService.GetPageModel(urlPath, localization, addIncludes);

        /// <summary>
        /// Gets a Page Model for a given Page Id.
        /// </summary>
        /// <param name="pageId">Page Id</param>
        /// <param name="localization">The context Localization.</param>
        /// <param name="addIncludes">Indicates whether include Pages should be expanded.</param>
        /// <returns>The Page Model.</returns>
        /// <exception cref="DxaItemNotFoundException">If no Page Model exists for the given Id.</exception>
        public virtual PageModel GetPageModel(int pageId, ILocalization localization, bool addIncludes = true)
            => _modelService.GetPageModel(pageId, localization, addIncludes);

        /// <summary>
        /// Gets an Entity Model for a given Entity Identifier.
        /// </summary>
        /// <param name="id">The Entity Identifier. Must be in format {ComponentID}-{TemplateID}.</param>
        /// <param name="localization">The context Localization.</param>
        /// <returns>The Entity Model.</returns>
        /// <exception cref="DxaItemNotFoundException">If no Entity Model exists for the given URL.</exception>
        public virtual EntityModel GetEntityModel(string id, ILocalization localization)
            => _modelService.GetEntityModel(id, localization);

        /// <summary>
        /// Gets a Static Content Item for a given URL path.
        /// </summary>
        /// <param name="urlPath">The URL path (unescaped).</param>
        /// <param name="localization">The context Localization.</param>
        /// <returns>The Static Content Item.</returns>
        public virtual StaticContentItem GetStaticContentItem(string urlPath, ILocalization localization)
        {
            using (new Tracer(urlPath, localization))
            {
                string localFilePath = BinaryFileManager.Instance.GetCachedFile(urlPath, localization);

                return new StaticContentItem(
                    new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan),
                    MimeMapping.GetMimeMapping(localFilePath),
                    File.GetLastWriteTime(localFilePath),
                    Encoding.UTF8
                    );
            }
        }

        /// <summary>
        /// Gets a Static Content Item for a given Id.
        /// </summary>
        /// <param name="binaryId">The Id of the binary.</param>
        /// <param name="localization">The context Localization.</param>
        /// <returns>The Static Content Item.</returns>
        public virtual StaticContentItem GetStaticContentItem(int binaryId, ILocalization localization)
        {
            using (new Tracer(binaryId, localization))
            {
                string localFilePath = BinaryFileManager.Instance.GetCachedFile(binaryId, localization);

                return new StaticContentItem(
                    new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan),
                    MimeMapping.GetMimeMapping(localFilePath),
                    File.GetLastWriteTime(localFilePath),
                    Encoding.UTF8
                    );
            }
        }

        /// <summary>
        /// Populates a Dynamic List by executing the query it specifies.
        /// </summary>
        /// <param name="dynamicList">The Dynamic List which specifies the query and is to be populated.</param>
        /// <param name="localization">The context Localization.</param>
        public virtual void PopulateDynamicList(DynamicList dynamicList, ILocalization localization)
        {
            using (new Tracer(dynamicList, localization))
            {              
                SimpleBrokerQuery simpleBrokerQuery = dynamicList.GetQuery(localization) as SimpleBrokerQuery;
                if (simpleBrokerQuery == null)
                {
                    throw new DxaException($"Unexpected result from {dynamicList.GetType().Name}.GetQuery: {dynamicList.GetQuery(localization)}");
                }
            
                int start = simpleBrokerQuery.Start;
                simpleBrokerQuery.Cursor = CursorMap.GetCursor(dynamicList.Id, ref start);
                simpleBrokerQuery.Start = start;
                dynamicList.Start = start;
            
                var brokerQuery = new GraphQLQueryProvider();

                var components = brokerQuery.ExecuteQueryItems(simpleBrokerQuery).ToList();
                Log.Debug($"Broker Query returned {components.Count} results. HasMore={brokerQuery.HasMore}");

                if (components.Count > 0)
                {
                    Type resultType = dynamicList.ResultType;
                    dynamicList.QueryResults = components
                        .Select(
                            c =>
                                ModelBuilderPipeline.CreateEntityModel(
                                    CreateEntityModelData((PublicContentApi.ContentModel.Component) c), resultType,
                                    localization))
                        .ToList();
                }

                dynamicList.HasMore = brokerQuery.HasMore;

                if (brokerQuery.HasMore)
                {
                    CursorMap.SetCursor(dynamicList.Id, simpleBrokerQuery.Start + simpleBrokerQuery.PageSize,
                        brokerQuery.Cursor);
                }
            }
        }

        protected virtual EntityModelData CreateEntityModelData(PublicContentApi.ContentModel.Component component)
        {
            ContentModelData standardMeta = new ContentModelData();
            foreach (var meta in component.CustomMetas.Edges)
            {
                standardMeta.Add(meta.Node.Key, meta.Node.Value);
            }

            // The semantic mapping requires that some metadata fields exist. This may not be the case so we map some component meta properties onto them
            // if they don't exist.
            if (!standardMeta.ContainsKey("dateCreated"))
            {
                standardMeta.Add("dateCreated", component.LastPublishDate);
            }
            const string dateTimeFormat = "MM/dd/yyyy HH:mm:ss";
            standardMeta["dateCreated"] = DateTime.ParseExact((string)standardMeta["dateCreated"], dateTimeFormat, null);
            if (!standardMeta.ContainsKey("name"))
            {
                standardMeta.Add("name", component.Title);
            }            
            return new EntityModelData
            {               
                Id = component.ItemId.ToString(),
                SchemaId = component.SchemaId.ToString(),
                Metadata = new ContentModelData { { "standardMeta", standardMeta } }
            };
        }

        string IRawDataProvider.GetPageContent(string urlPath, ILocalization localization)
        {
            // TODO: let the DXA Model Service provide raw Page Content too (?)
            using (new Tracer(urlPath, localization))
            {
                if (!urlPath.EndsWith(Constants.DefaultExtension) && !urlPath.EndsWith(".json"))
                {
                    urlPath += Constants.DefaultExtension;
                }
                string escapedUrlPath = Uri.EscapeUriString(urlPath);
                global::Tridion.ContentDelivery.DynamicContent.Query.Query brokerQuery = new global::Tridion.ContentDelivery.DynamicContent.Query.Query
                {
                    Criteria = CriteriaFactory.And(new Criteria[]
                    {
                        new PageURLCriteria(escapedUrlPath),
                        new PublicationCriteria(Convert.ToInt32(localization.Id)),
                        new ItemTypeCriteria(64)
                    })
                };

                string[] pageUris = brokerQuery.ExecuteQuery();
                if (pageUris.Length == 0)
                {
                    return null;
                }
                if (pageUris.Length > 1)
                {
                    throw new DxaException($"Broker Query for Page URL path '{urlPath}' in Publication '{localization.Id}' returned {pageUris.Length} results.");
                }

                PageContentAssembler pageContentAssembler = new PageContentAssembler();
                return pageContentAssembler.GetContent(pageUris[0]);
            }
        }
    }
}
