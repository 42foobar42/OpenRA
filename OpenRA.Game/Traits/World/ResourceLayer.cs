#region Copyright & License Information
/*
 * Copyright 2007-2010 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made 
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see LICENSE.
 */
#endregion

using System;
using System.Drawing;
using System.Linq;
using OpenRA.Graphics;

namespace OpenRA.Traits
{
	public class ResourceLayerInfo : TraitInfo<ResourceLayer> { }

	public class ResourceLayer: IRenderOverlay, ILoadWorldHook, ITerrainTypeModifier
	{		
		SpriteRenderer sr;
		World world;

		public ResourceType[] resourceTypes;
		CellContents[,] content;

		public ResourceLayer()
		{
			sr = Game.renderer.SpriteRenderer;
		}
		
		public void Render()
		{
			var cliprect = Game.viewport.ShroudBounds().HasValue
				? Rectangle.Intersect(Game.viewport.ShroudBounds().Value, world.Map.Bounds) : world.Map.Bounds;

			var minx = cliprect.Left;
			var maxx = cliprect.Right;

			var miny = cliprect.Top;
			var maxy = cliprect.Bottom;

			for (int x = minx; x < maxx; x++)
				for (int y = miny; y < maxy; y++)
				{
					if (world.LocalPlayer != null &&
				    		!world.LocalPlayer.Shroud.IsExplored(new int2(x, y)))
							continue;

					var c = content[x, y];
					if (c.image != null)
						sr.DrawSprite(c.image[c.density],
							Game.CellSize * new int2(x, y),
							c.type.info.Palette);
				}
		}

		public void WorldLoaded(World w)
		{
			this.world = w;
			content = new CellContents[w.Map.MapSize.X, w.Map.MapSize.Y];

			resourceTypes = w.WorldActor.traits.WithInterface<ResourceType>().ToArray();
			foreach (var rt in resourceTypes)
				rt.info.Sprites = rt.info.SpriteNames.Select(a => SpriteSheetBuilder.LoadAllSprites(a)).ToArray();

			var map = w.Map;

			for (int x = map.XOffset; x < map.XOffset + map.Width; x++)
				for (int y = map.YOffset; y < map.YOffset + map.Height; y++)
				{
					content[x, y].type = resourceTypes.FirstOrDefault(
						r => r.info.ResourceType == w.Map.MapResources[x, y].type);
					if (content[x, y].type != null)
						content[x, y].image = ChooseContent(content[x, y].type);
				}

			for (int x = map.XOffset; x < map.XOffset + map.Width; x++)
				for (int y = map.YOffset; y < map.YOffset + map.Height; y++)
					if (content[x, y].type != null)
						content[x, y].density = GetIdealDensity(x, y);
		}
		
		public string GetTerrainType(int2 cell)
		{
			if (content[cell.X,cell.Y].type == null)
				return null;
			
			return content[cell.X,cell.Y].type.info.TerrainType;
		}
				
		Sprite[] ChooseContent(ResourceType t)
		{
			return t.info.Sprites[world.SharedRandom.Next(t.info.Sprites.Length)];
		}

		int GetAdjacentCellsWith(ResourceType t, int i, int j)
		{
			int sum = 0;
			for (var u = -1; u < 2; u++)
				for (var v = -1; v < 2; v++)
					if (content[i+u, j+v].type == t)
						++sum;
			return sum;
		}

		int GetIdealDensity(int x, int y)
		{
			return (GetAdjacentCellsWith(content[x, y].type, x, y) *
				(content[x, y].image.Length - 1)) / 9;
		}

		public void AddResource(ResourceType t, int i, int j, int n)
		{
			if (content[i, j].type == null)
			{
				content[i, j].type = t;
				content[i, j].image = ChooseContent(t);
				content[i, j].density = -1;
			}

			if (content[i, j].type != t)
				return;

			content[i, j].density = Math.Min(
				content[i, j].image.Length - 1, 
				content[i, j].density + n);
		}

		public bool IsFull(int i, int j) { return content[i, j].density == content[i, j].image.Length - 1; }

		public ResourceType Harvest(int2 p)
		{
			var type = content[p.X,p.Y].type;
			if (type == null) return null;

			if (--content[p.X, p.Y].density < 0)
			{
				content[p.X, p.Y].type = null;
				content[p.X, p.Y].image = null;
			}
			return type;
		}

		public void Destroy(int2 p)
		{
			content[p.X, p.Y].type = null;
			content[p.X, p.Y].image = null;
			content[p.X, p.Y].density = 0;
		}

		public ResourceType GetResource(int2 p) { return content[p.X, p.Y].type; }

		public struct CellContents
		{
			public ResourceType type;
			public Sprite[] image;
			public int density;
		}
	}
}
