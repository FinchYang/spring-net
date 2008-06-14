#region License

/*
 * Copyright 2002-2004 the original author or authors.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#endregion

#region Imports

using System;
using System.Collections;
using System.Collections.Specialized;
using System.Web.UI;
using Spring.Validation;
using Spring.Web.UI.Controls;
using IValidator = Spring.Validation.IValidator;

#if NET_2_0
using Spring.Context;
using Spring.Context.Support;
using Spring.Globalization;
using Spring.Util;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Web;
using System.Web.Compilation;
using Spring.Web.Support;
#endif

#endregion Imports

namespace Spring.Web.UI
{
#if !NET_2_0
    
    #region ASP.NET 1.1 Spring Master Page Implementation

    /// <summary>
    /// Spring.NET Master Page implementation for ASP.NET 1.1
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class MasterPage : UserControl
    {
        /// <summary>
        /// Initializes master page.
        /// </summary>
        public void Initialize(Page childPage)
        {
            InitializeAsUserControl(childPage);
            this.ID = "masterPage";

            Control[] controls = new Control[childPage.Controls.Count];
            childPage.Controls.CopyTo(controls, 0);

            for (int i = 0; i < controls.Length; i++)
            {
                if (controls[i] is Content)
                {
                    Content content = (Content) controls[i];
                    ContentPlaceHolder placeholder = (ContentPlaceHolder) this.FindControl(content.ContentPlaceHolderID);
                    if (placeholder == null)
                    {
                        throw new ArgumentException("Content placeholder " + content.ContentPlaceHolderID + " does not exist in the master page.");
                    }
                    
                    placeholder.Content = content;
                }
            }

            childPage.Controls.AddAt(0, this);
        }


		/// <summary>
		/// Delegate validation errors to the owning page.
		/// </summary>
    	public override IValidationErrors ValidationErrors
    	{
    		get { return Page.ValidationErrors; }
    	}
    }

    #endregion    

#else

    #region ASP.NET 2.0 Spring Master Page Implementation

    
    /// <summary>
    /// Spring.NET Master Page implementation for ASP.NET 2.0
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class MasterPage : System.Web.UI.MasterPage, IApplicationContextAware, ISupportsWebDependencyInjection
    {
        #region Instance Fields

        private ILocalizer localizer;
        private IValidationErrors validationErrors = new ValidationErrors();
        private IMessageSource messageSource;
        private IApplicationContext applicationContext;
        private IApplicationContext defaultApplicationContext;

        #endregion

        #region Control lifecycle methods
        
        /// <summary>
        /// Initializes user control.
        /// </summary>
        protected override void OnInit(EventArgs e)
        {
            InitializeMessageSource();
            
            base.OnInit(e);

            // initialize controls
            OnInitializeControls(EventArgs.Empty);            
        }

        /// <summary>
        /// Binds data from the data model into controls and raises
        /// PreRender event afterwards.
        /// </summary>
        /// <param name="e">Event arguments.</param>
        protected override void OnPreRender(EventArgs e)
        {
            if (localizer != null)
            {
                localizer.ApplyResources(this, messageSource, UserCulture);
            }
            else if (Page.Localizer != null)
            {
                Page.Localizer.ApplyResources(this, messageSource, UserCulture);
            }

            base.OnPreRender(e);
        }

        /// <summary>
        /// This event is raised before Load event and should be used to initialize
        /// controls as necessary.
        /// </summary>
        public event EventHandler InitializeControls;

        /// <summary>
        /// Raises InitializeControls event.
        /// </summary>
        /// <param name="e">Event arguments.</param>
        protected virtual void OnInitializeControls(EventArgs e)
        {
            if (InitializeControls != null)
            {
                InitializeControls(this, e);
            }
        }

        /// <summary>
        /// Obtains a <see cref="T:System.Web.UI.UserControl"/> object from a user control file
        /// and injects dependencies according to Spring config file.
        /// </summary>
        /// <param name="virtualPath">The virtual path to a user control file.</param>
        /// <returns>
        /// Returns the specified <see langword="UserControl"/> object, with dependencies injected.
        /// </returns>
        protected new Control LoadControl(string virtualPath)
        {
            Control control = base.LoadControl(virtualPath);
            WebDependencyInjectionUtils.InjectDependenciesRecursive(defaultApplicationContext,control);
            return control;
        }

        /// <summary>
        /// Obtains a <see cref="T:System.Web.UI.UserControl"/> object by type
        /// and injects dependencies according to Spring config file.
        /// </summary>
        /// <param name="t">The type of a user control.</param>
        /// <param name="parameters">parameters to pass to the control</param>
        /// <returns>
        /// Returns the specified <see langword="UserControl"/> object, with dependencies injected.
        /// </returns>
        protected new Control LoadControl( Type t, params object[] parameters)
        {
            Control control = base.LoadControl( t, parameters );
            WebDependencyInjectionUtils.InjectDependenciesRecursive(defaultApplicationContext,control);
            return control;
        }

        #endregion Control lifecycle methods

        #region Data binding events

        /// <summary>
        /// This event is raised after all controls have been populated with values
        /// from the data model.
        /// </summary>
        public event EventHandler DataBound;

        /// <summary>
        /// Raises DataBound event.
        /// </summary>
        /// <param name="e">Event arguments.</param>
        protected virtual void OnDataBound(EventArgs e)
        {
            if (DataBound != null)
            {
                DataBound(this, e);
            }
        }

        /// <summary>
        /// This event is raised after data model has been populated with values from
        /// web controls.
        /// </summary>
        public event EventHandler DataUnbound;

        /// <summary>
        /// Raises DataBound event.
        /// </summary>
        /// <param name="e">Event arguments.</param>
        protected virtual void OnDataUnbound(EventArgs e)
        {
            if (DataUnbound != null)
            {
                DataUnbound(this, e);
            }
        }

        #endregion

        #region Application context support

        /// <summary>
        /// Gets or sets Spring application context.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public virtual IApplicationContext ApplicationContext
        {
            get { return applicationContext; }
            set { applicationContext = value; }
        }

        #endregion

        #region Message source and localization support

        /// <summary>
        /// Gets or sets the localizer.
        /// </summary>
        /// <value>The localizer.</value>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ILocalizer Localizer
        {
            get { return localizer; }
            set
            {
                localizer = value;
                if (localizer.ResourceCache is NullResourceCache)
                {
                    localizer.ResourceCache = new AspNetResourceCache();
                }
            }
        }

        /// <summary>
        /// Gets or sets the local message source.
        /// </summary>
        /// <value>The local message source.</value>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IMessageSource MessageSource
        {
            get { return messageSource; }
            set
            {
                messageSource = value;
                if (messageSource != null && messageSource is AbstractMessageSource)
                {
                    ((AbstractMessageSource) messageSource).ParentMessageSource = applicationContext;
                }
            }
        }

        /// <summary>
        /// Initializes local message source
        /// </summary>
        protected void InitializeMessageSource()
        {
            if (MessageSource == null)
            {
                string key = GetType().FullName + ".MessageSource";
                MessageSource = (IMessageSource) Context.Cache.Get(key);

                if (MessageSource == null)
                {
                    ResourceSetMessageSource defaultMessageSource = new ResourceSetMessageSource();
                    ResourceManager rm = GetLocalResourceManager();
                    if (rm != null)
                    {
                        defaultMessageSource.ResourceManagers.Add(rm);
                    }
                    Context.Cache.Insert(key, defaultMessageSource);
                    MessageSource = defaultMessageSource;
                }
            }
        }

        /// <summary>
        /// Creates and returns local ResourceManager for this page.
        /// </summary>
        /// <remarks>
        /// <para>
        /// In ASP.NET 1.1, this method loads local resources from the web application assembly.
        /// </para>
        /// <para>
        /// However, in ASP.NET 2.0, local resources are compiled into the dynamic assembly, 
        /// so we need to find that assembly instead and load the resources from it.
        /// </para>
        /// </remarks>
        /// <returns>Local ResourceManager instance.</returns>
        private ResourceManager GetLocalResourceManager()
        {
            object resourceProvider = Page.GetLocalResourceProvider.Invoke(typeof(ResourceExpressionBuilder), new object[] {this});
            MethodInfo GetLocalResourceAssembly =
                    resourceProvider.GetType().GetMethod("GetLocalResourceAssembly", BindingFlags.NonPublic | BindingFlags.Instance);
            Assembly localResourceAssembly = (Assembly) GetLocalResourceAssembly.Invoke(resourceProvider, null);
            if (localResourceAssembly != null)
            {
                return new ResourceManager(VirtualPathUtility.GetFileName(this.AppRelativeVirtualPath), localResourceAssembly);
            }
            return null;
        }

        /// <summary>
        /// Returns message for the specified resource name.
        /// </summary>
        /// <param name="name">Resource name.</param>
        /// <returns>Message text.</returns>
        public string GetMessage(string name)
        {
            return messageSource.GetMessage(name, UserCulture);
        }

        /// <summary>
        /// Returns message for the specified resource name.
        /// </summary>
        /// <param name="name">Resource name.</param>
        /// <param name="args">Message arguments that will be used to format return value.</param>
        /// <returns>Formatted message text.</returns>
        public string GetMessage(string name, params object[] args)
        {
            return messageSource.GetMessage(name, UserCulture, args);
        }

        /// <summary>
        /// Returns resource object for the specified resource name.
        /// </summary>
        /// <param name="name">Resource name.</param>
        /// <returns>Resource object.</returns>
        public object GetResourceObject(string name)
        {
            return messageSource.GetResourceObject(name, UserCulture);
        }

        /// <summary>
        /// Gets or sets user's culture
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public virtual CultureInfo UserCulture
        {
            get { return Page.UserCulture; }
            set { Page.UserCulture = value; }
        }

        #endregion

        #region Validation support

        /// <summary>
        /// Evaluates specified validators and returns <c>True</c> if all of them are valid.
        /// </summary>
        /// <remarks>
        /// <p>
        /// Each validator can itself represent a collection of other validators if it is
        /// an instance of <see cref="ValidatorGroup"/> or one of its derived types.
        /// </p>
        /// <p>
        /// Please see the Validation Framework section in the documentation for more info.
        /// </p>
        /// </remarks>
        /// <param name="validationContext">Object to validate.</param>
        /// <param name="validators">Validators to evaluate.</param>
        /// <returns>
        /// <c>True</c> if all of the specified validators are valid, <c>False</c> otherwise.
        /// </returns>
        public virtual bool Validate(object validationContext, params IValidator[] validators)
        {
            IDictionary contextParams = CreateValidatorParameters();
            bool result = true;
            foreach (IValidator validator in validators)
            {
                if (validator == null)
                {
                    throw new ArgumentException("Validator is not defined.");
                }
                result = validator.Validate(validationContext, contextParams, this.validationErrors) && result;
            }

            return result;
        }

        /// <summary>
        /// Gets the validation errors container.
        /// </summary>
        /// <value>The validation errors container.</value>
        public virtual IValidationErrors ValidationErrors
        {
            get { return validationErrors; }
        }

        /// <summary>
        /// Creates the validator parameters.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method can be overriden if you want to pass additional parameters
        /// to the validation framework, but you should make sure that you call
        /// this base implementation in order to add page, session, application,
        /// request, response and context to the variables collection.
        /// </para>
        /// </remarks>
        /// <returns>
        /// Dictionary containing parameters that should be passed to
        /// the data validation framework.
        /// </returns>
        protected virtual IDictionary CreateValidatorParameters()
        {
            IDictionary parameters = new ListDictionary();
            parameters["page"] = this.Page;
            parameters["usercontrol"] = this;
            parameters["session"] = this.Session;
            parameters["application"] = this.Application;
            parameters["request"] = this.Request;
            parameters["response"] = this.Response;
            parameters["context"] = this.Context;

            return parameters;
        }

        #endregion

        #region Spring Page support

        /// <summary>
        /// Overrides Page property to return <see cref="Spring.Web.UI.Page"/> 
        /// instead of <see cref="System.Web.UI.Page"/>.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new Page Page
        {
            get { return (Page) base.Page; }
        }

        #endregion
    
        #region Dependency Injection Support

        /// <summary>
        /// Holds the default ApplicationContext to be used during DI.
        /// </summary>
        IApplicationContext ISupportsWebDependencyInjection.DefaultApplicationContext
        {
            get { return defaultApplicationContext; }
            set { defaultApplicationContext = value; }
        }

        /// <summary>
        /// Injects dependencies before adding the control.
        /// </summary>
        protected override void AddedControl(Control control,int index)
        {
            WebDependencyInjectionUtils.InjectDependenciesRecursive(defaultApplicationContext,control);
            base.AddedControl(control,index);
        }

		#endregion Dependency Injection Support
    }

    #endregion

#endif
}