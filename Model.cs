using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Survarium_Simple_Importer 
{
	class Model 
	{
		public string Mesh_name { get;set; }
		public string Texture_name { get;set; }
		public Vector3[] Normals{ get;set; }
		public short[] Faces { get;set; }
		public Vector3[] Vertices { get;set; }
		public Vector2[] UV { get;set; }
		public bool IsLoaded { get;set; }

	}

}
