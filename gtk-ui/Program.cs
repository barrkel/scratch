using System;
using Gtk;

public class GtkHelloWorld
{
	public static void Main()
	{
		Application.Init();

		Window mainWindow = new Window("GTK ScratchPad");
		mainWindow.Resize(600, 600);

		Label tabLabel = new Label();
		tabLabel.Text = "First Tab";
		
		TextView text = new TextView { WrapMode = WrapMode.Word };
		text.Buffer.Text = "Hello there";

		ScrolledWindow scrolled = new ScrolledWindow();
		scrolled.Add(text);

		Frame frame = new Frame();
		frame.Add(scrolled);
		
		Notebook notebook = new Notebook();
		notebook.AppendPage(frame, tabLabel);

		Label location = new Label
		{
			Text = "Some Location",
			Justify = Justification.Left,
			Wrap = true,
		};
		location.SetAlignment(0, 0);
		location.SetPadding(20, 20);

		VBox box = new VBox();
		box.PackStart(notebook, true, true, 0);
		box.PackEnd(location, false, false, 0);
		
		mainWindow.Add(box);
		
		mainWindow.ShowAll();

		mainWindow.Destroyed += (o, e) => { Application.Quit(); };

		mainWindow.KeyPressEvent += new KeyPressEventHandler(myWin_KeyPressEvent);
		
		Application.Run();
	}

	static void myWin_KeyPressEvent(object o, KeyPressEventArgs args)
	{
		Console.WriteLine(args.Event.Key);
		Console.WriteLine(args.Event.State);
	}
}
