using System;
using System.Collections.Generic;

namespace ShapeKeyTools
{
	[Serializable]
	public class ShapeKeyDataDto
	{
		public string name;
		public float weight;
		public bool isLocked;
		public bool isExtended;
		public string originalName;
		public float minValue;
		public float maxValue;
	}

	[Serializable]
	public class GroupDataDto
	{
		public string groupName;
		public List<ShapeKeyDataDto> shapeKeys = new List<ShapeKeyDataDto>();
	}

	[Serializable]
	public class ShapeKeyStateDto
	{
		public List<GroupDataDto> groups = new List<GroupDataDto>();
		public Dictionary<string, bool> groupFoldouts = new Dictionary<string, bool>();
		public Dictionary<string, float> groupTestSliders = new Dictionary<string, float>();
		public Dictionary<int, bool> lockedShapeKeys = new Dictionary<int, bool>();
	}
}


