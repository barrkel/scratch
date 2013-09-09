using System.IO;
using System.Collections.Generic;
using System;


namespace Barrkel.GtkScratchPad
{
	public class Settings
	{
		Dictionary<string,string> _dict = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
		
		public Settings() : this(null)
		{
		}
		
		public Settings(TextReader source)
		{
			if (source != null)
			{
				string line;
				while ((line = source.ReadLine()) != null)
				{
					int split = line.IndexOf('=');
					if (split < 0)
						continue;
					this[line.Substring(0, split)] = line.Substring(split + 1);
				}
			}
		}
		
		public string this[string key]
		{
			get
			{
				if (_dict.ContainsKey(key))
					return _dict[key];
				return null;
			}
			set
			{
				if (_dict.ContainsKey(key))
					_dict[key] = value;
				else
					_dict.Add(key, value);
			}
		}
		
		public string Get(string key, string valueIfMissing)
		{
			return _dict.ContainsKey(key) ? _dict[key] : valueIfMissing;
		}
	}
}
