﻿using DD4T.ContentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Sdl.Web.Mvc.Models;
using Sdl.Web.Mvc.Mapping;
using Sdl.Web.DD4T.Extensions;
using System.Text.RegularExpressions;
using System.Collections;
using DD4T.ContentModel.Factories;
using DD4T.Factories;

namespace Sdl.Web.DD4T
{
    /// <summary>
    /// Default ModelFactory, retrieves model type and sets properties
    /// according to some generic rules
    /// </summary>
    public class DD4TModelFactory : BaseModelFactory
    {
        public ExtensionlessLinkFactory LinkFactory { get; set; }
        public DD4TModelFactory()
        {
            this.LinkFactory = new ExtensionlessLinkFactory();
        }
        public override object CreateEntityModel(object data, string view = null)
        {
            IComponent component = data as IComponent;
            if (component==null && data is IComponentPresentation)
            {
                component = ((IComponentPresentation)data).Component;
            }
            if (component != null)
            {
                //TODO, handle more than just image MM components
                object model;
                if (component.Multimedia != null)
                {
                    model = GetImages(new List<IComponent> { component })[0];
                }
                else
                {
                    var entityType = component.Schema.RootElementName;
                    model = GetEntity(entityType);
                    var type = model.GetType();
                    foreach (var field in component.Fields)
                    {
                        SetProperty(model, field.Value);
                    }
                    foreach (var field in component.MetadataFields)
                    {
                        SetProperty(model, field.Value);
                    }
                }
                return model;
            }
            else
            {
                throw new Exception(String.Format("Cannot create model for class {0}. Expecting IComponentPresentation/IComponent.", data.GetType().FullName));
            }
        }

        public override object CreatePageModel(object data, string view)
        {
            IPage page = data as IPage;
            if (page != null)
            {
                WebPage model = new WebPage{Id=page.Id,Title=page.Title};
                //TODO we need some logic to set the header, footer, breadcrumb etc.
                foreach (var cp in page.ComponentPresentations)
                {
                    string regionName = GetRegionFromComponentPresentation(cp);
                    if (!model.Regions.ContainsKey(regionName))
                    {
                        model.Regions.Add(regionName, new Region { Name = regionName });
                    }
                    model.Regions[regionName].Items.Add(cp);
                }
                return model;
            }
            throw new Exception(String.Format("Cannot create model for class {0}. Expecting IPage.", data.GetType().FullName));
        }

        private string GetRegionFromComponentPresentation(IComponentPresentation cp)
        {
            var match = Regex.Match(cp.ComponentTemplate.Title,@".*?\[(.*?)\]");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            //default region name
            return "Main";
        }

        public void SetProperty(object model, IField field)
        {
            if (field.Values.Count > 0 || (field.EmbeddedValues!=null && field.EmbeddedValues.Count > 0))
            {
                PropertyInfo pi = model.GetType().GetProperty(field.Name.Substring(0, 1).ToUpper() + field.Name.Substring(1));
                if (pi != null)
                {
                    //TODO check/cast to the type we are mapping to 
                    bool multival = pi.PropertyType.IsGenericType && (pi.PropertyType.GetGenericTypeDefinition() == typeof(List<>));
                    switch (field.FieldType)
                    {
                        case (FieldType.Date):
                            pi.SetValue(model, GetDates(field, pi.PropertyType, multival));
                            break;
                        case (FieldType.Number):
                            pi.SetValue(model, GetNumbers(field, pi.PropertyType, multival));
                            break;
                        case (FieldType.MultiMediaLink):
                            pi.SetValue(model, GetMultiMediaLinks(field, pi.PropertyType, multival));
                            break;
                        case (FieldType.ComponentLink):
                            pi.SetValue(model, GetMultiComponentLinks(field, pi.PropertyType, multival));
                            break;
                        case (FieldType.Embedded):
                            pi.SetValue(model, GetMultiEmbedded(field, pi.PropertyType, multival));
                            break;
                        default:
                            pi.SetValue(model, GetStrings(field, pi.PropertyType, multival));
                            break;
                    }
                }
            }
        }

        private object GetDates(IField field, Type modelType, bool multival)
        {
            if (typeof(DateTime).IsAssignableFrom(modelType))
            {
                if (multival)
                {
                    return field.DateTimeValues;
                }
                else
                {
                    return field.DateTimeValues[0];
                }
            }
            return null;
        }

        private object GetNumbers(IField field, Type modelType, bool multival)
        {
            if (typeof(Double).IsAssignableFrom(modelType))
            {
                if (multival)
                {
                    return field.NumericValues;
                }
                else
                {
                    return field.NumericValues[0];
                }
            }
            return null;
        }

        private object GetMultiMediaLinks(IField field, Type modelType, bool multival)
        {
            if (typeof(Image).IsAssignableFrom(modelType))
            {
                if (multival)
                {
                    return GetImages(field.LinkedComponentValues);
                }
                else
                {
                    return GetImages(field.LinkedComponentValues)[0];
                }
            }
            return null;
        }


        private object GetMultiComponentLinks(IField field, Type modelType, bool multival)
        {
            if (multival)
            {
                return GetCompLinks(field.LinkedComponentValues);
            }
            else
            {
                return GetCompLinks(field.LinkedComponentValues)[0];
            }
        }

        private object GetMultiEmbedded(IField field, Type modelType, bool multival)
        {
            if (typeof(Link).IsAssignableFrom(modelType))
            {
                var links = GetLinks(field.EmbeddedValues);
                if (multival)
                {
                    return links;
                }
                else
                {
                    
                    return links.Count > 0 ? links[0] : null;
                }
            }
            return null;
        }

        private object GetStrings(IField field, Type modelType, bool multival)
        {
            if (typeof(String).IsAssignableFrom(modelType))
            {
                if (multival)
                {
                    return field.Values;
                }
                else
                {
                    return field.Value;
                }
            }
            return null;
        }

        private List<Image> GetImages(IList<IComponent> components)
        {
            return components.Select(c => new Image { Url = c.Multimedia.Url, Id = c.Id, FileSize = c.Multimedia.Size }).ToList();
        }

        private List<object> GetCompLinks(IList<IComponent> components)
        {
            return components.Select(c => this.CreateEntityModel(c)).ToList();
        }

        private List<Link> GetLinks(IList<IFieldSet> list)
        {
            var result = new List<Link>();
            foreach (IFieldSet fs in list)
            {
                var link = new Link();
                link.AlternateText = fs.ContainsKey("alternateText") ? fs["alternateText"].Value : null;
                link.LinkText = fs.ContainsKey("linkText") ? fs["linkText"].Value : null;
                link.Url = fs.ContainsKey("externalLink") ? fs["externalLink"].Value : (fs.ContainsKey("internalLink") ? LinkFactory.ResolveExtensionlessLink(fs["internalLink"].LinkedComponentValues[0].Id) : null);
                if (!String.IsNullOrEmpty(link.Url))
                {
                    result.Add(link);
                }
            }
            return result;
        }


     }
}