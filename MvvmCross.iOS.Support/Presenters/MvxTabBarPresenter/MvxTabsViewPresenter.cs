﻿using System;
using System.Collections.Generic;
using System.Linq;
using MvvmCross.Core.ViewModels;
using MvvmCross.iOS.Support.Views;
using MvvmCross.iOS.Views;
using MvvmCross.iOS.Views.Presenters;
using MvvmCross.Platform;
using MvvmCross.Platform.Exceptions;
using UIKit;

namespace MvvmCross.iOS.Support.Presenters
{
	public class MvxTabsViewPresenter : MvxBaseIosViewPresenter, IMvxTabBarPresenter
	{
		public IMvxTabBarViewController TabBarViewController { get; set; }

		public UINavigationController NavigationController { get; set; }

		protected UIWindow Window { get; set; }

		protected UIApplicationDelegate AppDelegate { get; set; }

		public MvxTabsViewPresenter(UIApplicationDelegate appDelegate, UIWindow window) : base()
		{
			Window = window;
			AppDelegate = appDelegate;
		}

		public override void Show(MvxViewModelRequest request)
		{
			Show(this.CreateViewControllerFor(request), request);
		}

		public override void ChangePresentation(MvxPresentationHint hint)
		{
			base.ChangePresentation(hint);

			if(hint is MvxClosePresentationHint)
			{
				Close((hint as MvxClosePresentationHint).ViewModelToClose);
			}
		}

		public void Show(IMvxIosView view, MvxViewModelRequest request)
		{
			var viewController = GetViewController(view);

			var attribute = GetMvxTabPresentationAttribute(viewController);

			switch(attribute.Mode)
			{
				case MvxTabPresentationMode.Root:
					ShowRootViewController(viewController);
					break;
				case MvxTabPresentationMode.Tab:
					ShowTabViewController(viewController, attribute.WrapInNavigationController, attribute.TabTitle, attribute.TabIconName);
					break;
				case MvxTabPresentationMode.Child:
					ShowChildViewController(viewController);
					break;
				case MvxTabPresentationMode.Modal:
					ShowModalViewController(viewController, attribute.WrapInNavigationController);
					break;
			}
		}

		public virtual UIViewController GetCurrentViewController()
		{
			UIViewController currentViewController;

			// get current ViewController based on Root. 
			if(TabBarViewController != null)
				currentViewController = (TabBarViewController as UIViewController);
			else
				currentViewController = NavigationController.TopViewController;

			// check if there is a modal ViewController presented
			return currentViewController.PresentedViewController ?? currentViewController;
		}

		protected virtual void Close(IMvxViewModel toClose)
		{
			// check if toClose is a modal ViewController
			if(Window.RootViewController.PresentedViewController != null)
			{
				UIViewController viewController;
				// the presented ViewController might be a NavigationController as well
				var navigationController = Window.RootViewController.PresentedViewController as UINavigationController;
				viewController = navigationController != null
									? navigationController.TopViewController
									: Window.RootViewController.PresentedViewController;

				var mvxView = viewController.GetIMvxIosView();

				if(mvxView.ViewModel == toClose)
				{
					Window.RootViewController.DismissViewController(true, null);
					return;
				}
			}

			// check if ViewModel is shown inside TabBarViewController
			// if so, the TabBarViewController takes care of it
			if(TabBarViewController != null)
			{
				TabBarViewController.CloseChildViewModel(toClose);
				return;
			}

			// close ViewModel shown inside NavigationController
			if(NavigationController.TopViewController.GetIMvxIosView().ViewModel == toClose)
				NavigationController.PopViewController(true);
			else
				ClosePreviousController(toClose);
		}

		protected virtual void ShowRootViewController(UIViewController viewController)
		{
			// if viewController is a TabBarViewController, then update current. If not, clear it
			if(viewController is IMvxTabBarViewController)
			{
				TabBarViewController = viewController as IMvxTabBarViewController;
				NavigationController = null;

				// set RootViewController
				Window.RootViewController = viewController;
			}
			else
			{
				NavigationController = new UINavigationController(viewController);
				TabBarViewController = null;

				// set RootViewController
				foreach(var v in Window.Subviews)
					v.RemoveFromSuperview();

				Window.AddSubview(NavigationController.View);
				Window.RootViewController = NavigationController;
			}
		}

		protected virtual void ShowTabViewController(
			UIViewController viewController,
			bool needsNavigationController,
			string tabTitle,
			string tabIconName)
		{
			if(TabBarViewController == null)
				throw new MvxException("You need a TabBarViewController to show a ViewModel as a Tab!");

			if(string.IsNullOrEmpty(tabTitle) && string.IsNullOrEmpty(tabIconName))
				throw new MvxException("You need to set at least an icon or a title when trying to show a ViewModel as a Tab!");

			TabBarViewController.ShowTabView(viewController, needsNavigationController, tabTitle, tabIconName);
		}

		protected virtual void ShowChildViewController(UIViewController viewController)
		{
			// dismiss ModalViewController if shown
			Window.RootViewController?.DismissViewController(true, null);

			// if current RootViewController is a TabBarViewController, the TabBarViewController takes care of it
			if(TabBarViewController != null)
			{
				TabBarViewController?.ShowChildView(viewController);
				return;
			}

			// if current RootViewController is a NavigationController, push it
			if(NavigationController != null)
			{
				NavigationController.PushViewController(viewController, true);
				return;
			}

			// there is no Root, so let's start this way!
			Mvx.Trace(MvvmCross.Platform.Platform.MvxTraceLevel.Warning, $"Showing View type: {typeof(UIViewController).Name} as Root, but it's attributed as Child");
			ShowRootViewController(viewController);
		}

		protected virtual void ShowModalViewController(UIViewController viewController, bool needsNavigationController)
		{
			Window.RootViewController.PresentViewController(
				needsNavigationController ? new UINavigationController(viewController) : viewController,
				true,
				null);
		}

		private UIViewController GetViewController(IMvxIosView view)
		{
			var viewController = view as UIViewController;
			if(viewController == null)
				throw new MvxException("Trying to show a view that isn't a UIViewController!");

			return viewController;
		}

		private MvxTabPresentationAttribute GetMvxTabPresentationAttribute(UIViewController viewController)
		{
			var attributes = viewController.GetType().GetCustomAttributes(typeof(MvxTabPresentationAttribute), true).FirstOrDefault() as MvxTabPresentationAttribute;
			if(attributes == null)
				throw new MvxException("Please remember to set PresentationAttributes!");

			return attributes;
		}

		private void ClosePreviousController(IMvxViewModel toClose)
		{
			foreach(var viewController in NavigationController.ViewControllers)
			{
				var mvxView = viewController.GetIMvxIosView();
				if(mvxView.ViewModel == toClose)
				{
					var newViewControllers = NavigationController.ViewControllers.Where(v => v != viewController).ToArray();
					NavigationController.ViewControllers = newViewControllers;
					break;
				}
			}
		}
	}
}

