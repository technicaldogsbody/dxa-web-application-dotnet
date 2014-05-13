﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace Sdl.Web.Mvc.Mapping
{
    /// <summary>
    /// Custom Action Filter Attribute to enable controller actions to provide Domain model -> View model mapping via a Content Provider
    /// TODO - currently just for entity models - should we handle Page/Region model mapping too?
    /// </summary>
    public class MapModelAttribute : ActionFilterAttribute
    {
        public IContentProvider ContentProvider { get; set; }
        
        public override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            var viewResult = (ViewResult)filterContext.Result;
            var sourceModel = filterContext.Controller.ViewData.Model;
            var contentProvider = ContentProvider ?? ((BaseController)filterContext.Controller).ContentProvider;
            var viewName = String.IsNullOrEmpty(viewResult.ViewName) ? contentProvider.GetEntityViewName(sourceModel) : viewResult.ViewName;
            var viewEngineResult = ViewEngines.Engines.FindPartialView(filterContext.Controller.ControllerContext, viewName);
            if (viewEngineResult.View == null)
            {
                Log.Error("Could not find view {0} in locations: {1}",viewName, String.Join(",", viewEngineResult.SearchedLocations));
                throw new Exception(String.Format("Missing view: {0}",viewName));
            }
            else
            {
                if (!Configuration.ViewModelRegistry.ContainsKey(viewName))
                {
                    //This is the only way to get the view model type from the view and thus prevent the need to configure this somewhere
                    var path = ((BuildManagerCompiledView)viewEngineResult.View).ViewPath;
                    Configuration.AddViewModelToRegistry(viewName, path);
                }
                //If the content provider does not return a view model, then we use the source model
                var model = contentProvider.CreateEntityModel(sourceModel, Configuration.ViewModelRegistry[viewName]) ?? sourceModel;
                filterContext.Controller.ViewData.Model = model;
                viewResult.ViewName = viewName;
            }
        }
    }
}
