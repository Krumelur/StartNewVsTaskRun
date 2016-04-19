using System;

using UIKit;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ContextDemo
{
	public partial class ViewController : UIViewController
	{
		public ViewController (IntPtr handle) : base (handle)
		{
		}

		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();

			WriteContextInfo(string.Empty);

			// Shows how execution will continue on the UI thread.
			CreateButton("Continue on UI thread (Task.Run())", async () => await DemoTaskRun());

			// Shows how execution might be expected to continue on UI thread but is not.
			CreateButton("Continue on thread pool (Task.Run())", async () => await Task.Run(() => DemoTaskRun()));

			CreateButton("StartNew() as used by Task.Run()", () => {
				DemoStartNew(TaskScheduler.Current);
			});

			CreateButton("StartNew() will run on UI thread", () => {
				DemoStartNew(TaskScheduler.FromCurrentSynchronizationContext());
			});

			// Shows unexpected behavior when using StartNew().
			CreateButton("StartNew() with await wrong", () => {
				DemoStartNewWithAwaitGoneWrong();
			});

			// Fixes unexpected behavior by using "await await".
			CreateButton("StartNew() with await fixed", () => {
				DemoStartNewWithAwaitFixed();
			});

			// This shows what Task.Run() does internally and should make it clear *why* Task.Run() is the better option.
			CreateButton("StartNew() with await fixed but better :-)", () => {
				DemoStartNewWithAwaitFixedBetter();
			});
		}

		void WriteContextInfo (string msg, [CallerMemberName] string caller = null)
		{
			Console.WriteLine("--------------------------------");
			Console.WriteLine($"[{caller}]: {msg}");
			Console.WriteLine($"\tThread ID: {Thread.CurrentThread.ManagedThreadId}");
			Console.WriteLine($"\tMain thread? {!Thread.CurrentThread.IsThreadPoolThread}");
			// Contexts: there is not necessarily a context. A console app for instance does not have one at all. It will use the TaskScheduler and makes your work end up in the ThreadPool.
			// Captured by "await" if it is not NULL.
			var syncContextName = SynchronizationContext?.Current?.GetType()?.Name ?? "(n/a)";
			Console.WriteLine($"\tCurrent sync context: {syncContextName}");

			// Captured by "await" if current sync context is NULL. 
			var currentTaskSchedulerName = TaskScheduler?.Current?.GetType()?.Name ?? "(n/a)";
			Console.WriteLine($"\tCurrent task scheduler: {currentTaskSchedulerName}");

			var defaultTaskSchedulerName = TaskScheduler?.Default?.GetType()?.Name ?? "(n/a)";
			Console.WriteLine($"\tDefault task scheduler: {defaultTaskSchedulerName}");
		}

		async Task DemoTaskRun()
		{
			this.WriteContextInfo("Start");
			// Task.Run() is short for Task.Factory.StartNew() with TaskCreationOptions.DenyChildAttach and TaskScheduler.Default.
			await Task.Run(() => {
					Thread.Sleep(500);
					WriteContextInfo("In Task.Run()");
			});
			this.WriteContextInfo("End");
		}

		async Task DemoStartNew(TaskScheduler scheduler)
		{
			this.WriteContextInfo("Start");
			// http://blogs.msdn.com/b/pfxteam/archive/2011/10/24/10229468.aspx
			var t = Task.Factory.StartNew(() => {
				Thread.Sleep(500);
				WriteContextInfo("In Task.Factory.StartNew()");
			},
				CancellationToken.None,
				// Used by Task.Run()
				TaskCreationOptions.DenyChildAttach,
				// Set to TaskScheduler.Current when called from Task.Run().
				scheduler);	

			await t;

			this.WriteContextInfo("End");
		}

		async Task DemoStartNewWithAwaitGoneWrong()
		{
			this.WriteContextInfo("Start");
			// http://blogs.msdn.com/b/pfxteam/archive/2011/10/24/10229468.aspx
			Task<Task<int>> outerTask = Task.Factory.StartNew(async () => {
				WriteContextInfo("In Task.Factory.StartNew() begin...");
				await Task.Delay(1000);
				WriteContextInfo("In Task.Factory.StartNew() done.");
				return 42;
			});

			Task<int> res = await outerTask;

			// Outer task's result is a Task...not what we want. We would want to get the "42".
			Console.WriteLine("Result of outer task:" + res);

			this.WriteContextInfo("End");
		}

		async Task DemoStartNewWithAwaitFixed()
		{
			this.WriteContextInfo("Start");
			// http://blogs.msdn.com/b/pfxteam/archive/2011/10/24/10229468.aspx
			Task<Task<int>> outerTask = Task.Factory.StartNew(async () => {
				WriteContextInfo("In Task.Factory.StartNew() begin...");
				await Task.Delay(1000);
				WriteContextInfo("In Task.Factory.StartNew() done.");
				return 42;
			}, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);

			//  "await await" looking strange? :-) Yes, but it's a Task<Task>().
			int res = await await outerTask;

			// Now we get the "42"!
			Console.WriteLine("Result of outer task:" + res);

			this.WriteContextInfo("End");
		}

		async Task DemoStartNewWithAwaitFixedBetter()
		{
			this.WriteContextInfo("Start");
			// http://blogs.msdn.com/b/pfxteam/archive/2011/10/24/10229468.aspx
			Task<Task<int>> outerTask = Task.Factory.StartNew(async () => {
				WriteContextInfo("In Task.Factory.StartNew() begin...");
				await Task.Delay(1000);
				WriteContextInfo("In Task.Factory.StartNew() done.");
				return 42;
			}, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);

			// This is what Task.Run() does. It extracts the inner Task and creates a proxy.
			var unwrappedTask = outerTask.Unwrap();
			int res = await unwrappedTask;

			// Now we get the "42"!
			Console.WriteLine("Result of outer task:" + res);

			this.WriteContextInfo("End");
		}

		void CreateButton(string title, Action callback)
		{
			var btn = new UIButton(UIButtonType.System);
			btn.SetTitle(title, UIControlState.Normal);
			btn.TouchUpInside += (sender, e) => callback();
			btn.Frame = new CoreGraphics.CGRect(20, offset, 300, 30);
			this.Add(btn);
			offset += 60;
		}

		 float offset = 40;

		public override void DidReceiveMemoryWarning ()
		{
			base.DidReceiveMemoryWarning ();
			// Release any cached data, images, etc that aren't in use.
		}
	}
}

