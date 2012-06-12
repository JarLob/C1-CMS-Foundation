﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.WebPages.Html;

using Composite.Core.Types;
using Composite.Core.Xml;
using Composite.Data.Types;

namespace Composite.AspNet.Razor
{
    /// <summary>
    /// Extension object to be used in Razor code
    /// </summary>
	public class C1HtmlHelper
	{
		private HtmlHelper _helper;

        /// <summary>
        /// Initializes a new instance of the <see cref="C1HtmlHelper"/> class.
        /// </summary>
        /// <param name="helper">The helper.</param>
		public C1HtmlHelper(HtmlHelper helper)
		{
			_helper = helper;
		}

        /// <summary>
        /// Returns a URL for a specific C1 page
        /// </summary>
        /// <param name="page">The page.</param>
        /// <returns></returns>
		public IHtmlString PageUrl(IPage page)
		{
			return PageUrl(page.Id.ToString());
		}

        /// <summary>
        /// Returns a URL for a specific C1 page
        /// </summary>
        /// <param name="page">The page.</param>
        /// <param name="querystring">The querystring object.</param>
        /// <returns></returns>
		public IHtmlString PageUrl(IPage page, object querystring)
		{
			return PageUrl(page.Id.ToString(), querystring);
		}

        /// <summary>
        /// Returns a URL for a specific C1 page
        /// </summary>
        /// <param name="page">The page.</param>
        /// <param name="querystring">An object which properties' values will be passes as query string.</param>
        /// <returns></returns>
		public IHtmlString PageUrl(IPage page, IDictionary<string, string> querystring)
		{
			return PageUrl(page.Id.ToString(), querystring);
		}


        /// <summary>
        /// Returns a URL for a specific C1 page
        /// </summary>
        /// <param name="pageId">The page id.</param>
        /// <param name="querystring">The querystring.</param>
        /// <returns></returns>
		public IHtmlString PageUrl(string pageId, object querystring = null)
		{
			var dict = Functions.ObjectToDictionary(querystring);

            return PageUrl(pageId, dict);
		}


        /// <summary>
        /// Returns a URL for a specific C1 page
        /// </summary>
        /// <param name="pageId">The page id.</param>
        /// <param name="querystring">The querystring.</param>
        /// <returns></returns>
		public IHtmlString PageUrl(string pageId, IDictionary<string, object> querystring)
		{
            string relativeUrl = "~/page(" + pageId + ")";
			string absoulteUrl = VirtualPathUtility.ToAbsolute(relativeUrl);

			if (querystring != null && querystring.Keys.Count > 0)
			{
                absoulteUrl += "?" + SerializeQueryString(querystring);
			}

			return _helper.Raw(absoulteUrl);
		}


        /// <summary>
        /// Returns a media url.
        /// </summary>
        /// <param name="mediaFile">The media file.</param>
        /// <returns></returns>
		public IHtmlString MediaUrl(IMediaFile mediaFile)
		{
			return MediaUrl(mediaFile.KeyPath);
		}


        /// <summary>
        /// Returns a media url.
        /// </summary>
        /// <param name="mediaFile">The media file.</param>
        /// <param name="querystring">The querystring.</param>
        /// <returns></returns>
		public IHtmlString MediaUrl(IMediaFile mediaFile, object querystring)
		{
			return MediaUrl(mediaFile.KeyPath, querystring);
		}


        /// <summary>
        /// Returns a media url.
        /// </summary>
        /// <param name="mediaFile">The media file.</param>
        /// <param name="querystring">The querystring.</param>
        /// <returns></returns>
		public IHtmlString MediaUrl(IMediaFile mediaFile, IDictionary<string, string> querystring)
		{
			return MediaUrl(mediaFile.KeyPath, querystring);
		}


        /// <summary>
        /// Returns a media url.
        /// </summary>
        /// <param name="mediaId">Id of a media file.</param>
        /// <param name="querystring">The querystring.</param>
        /// <returns></returns>
		public IHtmlString MediaUrl(Guid mediaId, object querystring = null)
		{
            return MediaUrl(mediaId.ToString(), querystring);
		}


        /// <summary>
        /// Returns a media url.
        /// </summary>
        /// <param name="mediaId">Id of a media file.</param>
        /// <param name="querystring">The querystring.</param>
        /// <returns></returns>
        public IHtmlString MediaUrl(Guid mediaId, IDictionary<string, string> querystring)
		{
            return MediaUrl(mediaId.ToString(), querystring);
		}


        /// <summary>
        /// Returns a media url.
        /// </summary>
        /// <param name="keyPath">The keyPath property of a media item.</param>
        /// <param name="querystring">The querystring.</param>
        /// <returns></returns>
        public IHtmlString MediaUrl(string keyPath, object querystring = null)
		{
			var dict = Functions.ObjectToDictionary(querystring);

			return MediaUrl(keyPath, dict);
		}
        

        /// <summary>
        /// Returns a media url.
        /// </summary>
        /// <param name="keyPath">The keyPath property of a media item.</param>
        /// <param name="querystring">The querystring.</param>
        /// <returns></returns>
		public IHtmlString MediaUrl(string keyPath, IDictionary<string, object> querystring)
		{
			string relativeUrl = "~/media(" + keyPath + ")";
			string absoulteUrl = VirtualPathUtility.ToAbsolute(relativeUrl);

			if (querystring != null && querystring.Keys.Count > 0)
			{
                absoulteUrl += "?" + SerializeQueryString(querystring);
			}

			return _helper.Raw(absoulteUrl);
		}


        private static string SerializeQueryString(IDictionary<string, object> querystring)
        {
            return String.Join("&amp;",
                querystring.Select(kvp => HttpUtility.UrlEncode(kvp.Key)
                                          + "=" + HttpUtility.UrlEncode(kvp.Value.ToString())));
        }



        /// <summary>
        /// Renders specified xhtml document.
        /// </summary>
        /// <param name="xhtmlDocument">The XHTML document.</param>
        /// <returns></returns>
		public IHtmlString Document(XhtmlDocument xhtmlDocument)
		{
			return _helper.Raw(xhtmlDocument.ToString());
		}

        /// <summary>
        /// Renders the &lt;body /&gt; part of the specified xhtml document.
        /// </summary>
        /// <param name="xhtmlDocument">The XHTML document.</param>
        /// <returns></returns>
		public IHtmlString Body(string xhtmlDocument)
		{
			var doc = XhtmlDocument.Parse(xhtmlDocument);

			return Body(doc);
		}

        /// <summary>
        /// Renders the &lt;body /&gt; part of the specified xhtml document.
        /// </summary>
        /// <param name="xhtmlDocument">The XHTML document.</param>
        /// <returns></returns>
		public IHtmlString Body(XhtmlDocument xhtmlDocument)
		{
			var body = xhtmlDocument.Descendants().SingleOrDefault(el => el.Name.LocalName == "body");
			if (body != null)
			{
				using (var reader = body.CreateReader())
				{
					reader.MoveToContent();

					return _helper.Raw(reader.ReadInnerXml());
				}
			}

			return Document(xhtmlDocument);
		}

        /// <summary>
        /// Executes a C1 function.
        /// </summary>
        /// <param name="name">Function name.</param>
        /// <returns></returns>
		public IHtmlString Function(string name)
		{
			return Function(name, null);
		}

        /// <summary>
        /// Executes a C1 function.
        /// </summary>
        /// <param name="name">Function name.</param>
        /// <param name="parameters">The parameters.</param>
        /// <returns></returns>
		public IHtmlString Function(string name, object parameters)
		{
			return Function(name, Functions.ObjectToDictionary(parameters));
		}

        /// <summary>
        /// Executes a C1 function.
        /// </summary>
        /// <param name="name">Function name.</param>
        /// <param name="parameters">The parameters.</param>
        /// <returns></returns>
		public IHtmlString Function(string name, IDictionary<string, object> parameters)
		{
			object result = Functions.ExecuteFunction(name, parameters);

			return ConvertFunctionResult(result);
		}

		private static IHtmlString ConvertFunctionResult(object result)
		{
			var resultAsString = ValueTypeConverter.Convert<string>(result);
			if (resultAsString != null)
			{
				return new HtmlString(resultAsString);
			}

			throw new InvalidOperationException("Function doesn't return string value");
		}
	}
}
