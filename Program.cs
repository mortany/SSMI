using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Survarium_Simple_Importer 
{
    class Program 
	{	

		static List<string> Params = new List<string>();
		static List<Model> Models = new List<Model>();
        static void Main(string[] args) 
		{
			OpenFile(args[0]);
        }
		
		private static float frac(float n)
		{
			return n - (float)Math.Floor(n);
		}

		private static float mad(float n)
		{			
			return (n * 2) -1;
		}

		private static float readHalf(BinaryReader reader)
		{
			byte[] bytes = new byte[2];
			bytes[0] = reader.ReadByte();
			bytes[1] = reader.ReadByte();

			return (float)Half.ToHalf(bytes,0);
		}

		private static Vector3 unpack_normals(float val)
		{						
			float X = val;
			float Y = val * 256;
			float Z = val * 65536;

			X = frac(X);
			Y = frac(Y);
			Z = frac(Z);

			X = mad(X);
			Y = mad(Y);
			Z = mad(Z);

			Vector3 tbn = new Vector3(-X,Y,Z);

			float n = Vector3.Dot(tbn,tbn);

			n = (float)Math.Sqrt(n);

			return tbn/n;
		}
		
		private static void OpenFile(string filename)
		{
			if(File.Exists(filename+"\\settings"))
			{
				Console.WriteLine("Setting was found.\nTry parse...");
				ParseSettings(filename+"\\settings");
				

				if(Models.Count > 0)
				{
					foreach(Model model in Models)
					{
						if(File.Exists(filename+"\\render\\" + model.Mesh_name + "\\converted_model"))
						{
							Console.WriteLine("Model was found.\nTry parse...");
							ParseModel(model,filename+"\\render\\" + model.Mesh_name + "\\converted_model");
						}
						else
						{
							model.IsLoaded = false;
						}
					}
				}
			
				if(Models.Count > 0)
				{
					Console.WriteLine("Models parsing successful. Save...");
					//Console.WriteLine("File "+Directory.GetParent(filename));

					foreach(Model model in Models)
					{
						if(model.IsLoaded)
						{
							Console.WriteLine("Saving "+model.Mesh_name +"...");						
							string lastFolderName = Path.GetFileName( filename );
							SaveFile(model,Directory.GetParent(filename)+"\\"+lastFolderName+"_converted\\");
						}
						else
						{
							Console.WriteLine("Model "+model.Mesh_name +" not found");
						}
						
					}
					
					Console.WriteLine("Complete!!!");
				}
			}
			else
			{
				Console.WriteLine("Setting not found.\nClose...");
				Console.ReadLine();
			}
			
		}

		private static string ReadSuperString(BinaryReader reader)
		{
			string str = "";
			char ch;
			while ((ch = (char) reader.ReadByte()) != char.MinValue)
			{
				str += ch.ToString();
			}			
			return str;

		}

		public static void ParseSettings(string path)
		{
			using (BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open)))
			{
				reader.BaseStream.Seek(24,SeekOrigin.Begin);
				short offset = reader.ReadInt16();
				reader.BaseStream.Seek(offset,SeekOrigin.Begin);
				
				List<string> meshes = new List<string>();
				List<string> textures = new List<string>();

				while (reader.BaseStream.Position != reader.BaseStream.Length)
				{
					string res = ReadSuperString(reader);

					Regex rgx1 = new Regex(@"^\w+_s$");
					Regex rgx2 = new Regex(@"\/\w+$");

					if(rgx1.IsMatch(res))
					{
						meshes.Add(res);
					}
					else if(rgx2.IsMatch(res))
					{
						res = Regex.Replace(res,@"\/",@"\");
						textures.Add(res);
					}

				}

				for(int i = 0; i < meshes.Count; i++)
				{
					Model model = new Model();
					model.Mesh_name = meshes[i];
					model.Texture_name = textures[i];
					Models.Add(model);
				}
			}
		}

		public static void ParseModel(Model model,string path)
		{
			using (BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open)))
			{
				reader.BaseStream.Seek(4,SeekOrigin.Begin);
				int size = reader.ReadInt32();
				int count = reader.ReadInt32();
				size -= 4;

				/*Model verison:
				 skin_1 = 28b // skip 0
				 skin_2 = 32b // skip 4
				 skin_3 = 36b // skip 8
				 skin_4 = 40b // skip 12
				 skin_5 = 44b // skip 16
				 Get offset : size - 28*/					

				int model_v = size / count;

				model.Vertices = new Vector3[count];
				model.Normals  = new Vector3[count];
				model.UV       = new Vector2[count];

				for (int i = 0; i < count; i++)
				{
					model.Vertices[i].X = -reader.ReadSingle();
					model.Vertices[i].Y = reader.ReadSingle();
					model.Vertices[i].Z = reader.ReadSingle();

					reader.ReadBytes(model_v - 28);
					model.Normals[i] = unpack_normals(reader.ReadSingle());
					
					//Normals[i].Z *= -1;
					reader.ReadSingle();
					reader.ReadSingle();// Skip tangets & binormals

					model.UV[i].X = readHalf(reader);
					model.UV[i].Y = 1.0f-readHalf(reader);
				}

				if(model_v == 28)
				{
					reader.ReadSingle(); // Skip 4 bytes
				}
				else
				{
					reader.ReadSingle(); // Skip 4 bytes
					int skip = reader.ReadInt32();
					long pos = reader.BaseStream.Position;
					reader.BaseStream.Seek(pos + (skip * 25)-12,SeekOrigin.Begin);
				}

				int face_size = reader.ReadInt32();
				int face_count = reader.ReadInt32();

				model.Faces = new short[face_count];

				for (int i = 0; i < face_count; i+=3)
					for (int j = 2; j >= 0; j--)
						model.Faces[i+j] = reader.ReadInt16();

				model.IsLoaded = true;
			}
		}

		private static void Write_(BinaryWriter writer,string var)
		{
			writer.Write(var.ToCharArray());
		}

		private static string FormatF(float value)
		{
			string res = String.Format(new CultureInfo("en-US"),"{0:f4}", value);
			return  res;
		}

		private static void SaveFile(Model model,string path)
		{
			if(!Directory.Exists(path))
				Directory.CreateDirectory(path);
						
			using(BinaryWriter writer = new BinaryWriter(File.Create(path + "\\"+model.Mesh_name+".mtl")))
			{
				Write_(writer,"newmtl " + model.Mesh_name + "\n");
				string[] new_path = Regex.Split(path, @"models\\");
				Write_(writer,"map_Kd " + new_path[0]+@"textures\"+model.Texture_name + ".dds");
			}

			using(BinaryWriter writer = new BinaryWriter(File.Create(path + "\\"+model.Mesh_name+".obj")))
			{
				Write_(writer,"mtllib "+model.Mesh_name+".mtl\n");
				Write_(writer,"\n");

				for(int i = 0; i < model.Vertices.Length; i++)
				{
					Write_(writer,"v " + FormatF(model.Vertices[i].X) + " " + FormatF(model.Vertices[i].Y) + " " + FormatF(model.Vertices[i].Z) +"\n");
				}

				Write_(writer,"\n");

				for(int i = 0; i < model.UV.Length; i++)
				{
					Write_(writer,"vt " + FormatF(model.UV[i].X) + " "+ FormatF(model.UV[i].Y)+"\n");
				}

				Write_(writer,"\n");

				for(int i = 0; i < model.Normals.Length; i++)
				{
					Write_(writer,"vn " + FormatF(model.Normals[i].X) + " " + FormatF(model.Normals[i].Y) + " " + FormatF(model.Normals[i].Z)+"\n");
				}

				Write_(writer,"\n");

				Write_(writer,"g "+model.Mesh_name+"\n");
				Write_(writer,"usemtl "+model.Mesh_name + "\n");

				

				for(int i = 0; i < model.Faces.Length; i+=3)
				{
					Write_(writer,"f " + String.Format("{0}/{0}/{0}",(model.Faces[i]+1)) + " " + String.Format("{0}/{0}/{0}",(model.Faces[i+1]+1))+" "+String.Format("{0}/{0}/{0}",(model.Faces[i+2]+1))+"\n");
				}
				
			}

		}
    }
}

