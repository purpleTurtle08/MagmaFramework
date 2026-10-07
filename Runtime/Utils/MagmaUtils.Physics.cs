using System.Collections.Generic;
using UnityEngine;

namespace MagmaFlow.Framework.Utils
{
	/// <summary>
	/// A collection of utility methods for the MagmaFlow Framework.
	/// This partial class handles physics queries that generate no garbage after their first call.
	/// </summary>
	public static partial class MagmaUtils
	{
		// Shared by all callers. Safe because Physics queries are main-thread only and these methods don't re-enter.
		// Created on first use, so nothing is allocated unless these methods are called.
		private static Collider[] s_overlapBuffer;
		private static RaycastHit[] s_lineOfSightHits;

		/// <summary>
		/// Finds the closest points (to the sourcePoint) on all colliders that are within the overlap sphere of this sourcePoint.
		/// <para>Colliders that contain the sourcePoint are skipped.</para>
		/// <para>Non-convex MeshColliders and TerrainColliders are NOT supported by Collider.ClosestPoint(), so they are skipped as well.</para>
		/// </summary>
		/// <param name="results">Points are appended to this list (it is created if null). Clear it yourself between calls.</param>
		/// <param name="sourcePoint">The center of the overlap sphere.</param>
		/// <param name="radius">The radius of the overlap sphere.</param>
		/// <param name="ignoreRoot">Colliders on this transform and its children are ignored (usually the caller's own transform).</param>
		/// <param name="numberOfPoints">Maximum number of colliders to check.</param>
		/// <param name="collisionLayerMask">The layers to check against.</param>
		/// <param name="ignoreTriggers">How to handle trigger colliders.</param>
		/// <param name="passThrough">If false, points hidden behind other colliders are discarded.</param>
		public static void GetOverlapContactPoints
		(
			ref List<Vector3> results,
			Vector3 sourcePoint,
			float radius,
			Transform ignoreRoot = null,
			int numberOfPoints = 4,
			LayerMask? collisionLayerMask = null,
			QueryTriggerInteraction ignoreTriggers = QueryTriggerInteraction.Ignore,
			bool passThrough = true
		)
		{
			// Grows only when a caller asks for more than any previous one, never per call
			if (s_overlapBuffer == null || s_overlapBuffer.Length < numberOfPoints) s_overlapBuffer = new Collider[numberOfPoints];

			int actualMask = collisionLayerMask ?? Physics.AllLayers;
			int noOfOverlappingColliders = Physics.OverlapSphereNonAlloc(sourcePoint, radius, s_overlapBuffer, actualMask, ignoreTriggers);

			// The shared buffer may be larger than this caller asked for, so cap to numberOfPoints
			int count = Mathf.Min(noOfOverlappingColliders, numberOfPoints);
			if (noOfOverlappingColliders >= numberOfPoints)
			{
				LogWarning($"{(ignoreRoot != null ? ignoreRoot.name : "GetOverlapContactPoints")} ::: Found {noOfOverlappingColliders} overlapping colliders, " +
						   $"which fills the limit of {numberOfPoints}. Some colliders may have been missed. " +
						   $"Increase numberOfPoints OR decrease the radius when calling GetOverlapContactPoints().");
			}

			if (results == null) results = new List<Vector3>(numberOfPoints);
			for (int i = 0; i < count; i++)
			{
				Collider target = s_overlapBuffer[i];
				if (IsIgnoredCollider(target, ignoreRoot)) continue;

				Vector3 pointToAdd = target.ClosestPoint(sourcePoint);
				if (sourcePoint == pointToAdd) continue;

				if (passThrough || HasLineOfSight(pointToAdd, sourcePoint, ignoreRoot, target.transform, collisionLayerMask, ignoreTriggers))
				{
					results.Add(pointToAdd);
				}
			}
		}

		/// <summary>
		/// Returns true if nothing blocks the straight line between two points.
		/// <para>Example, "can this enemy see the player": HasLineOfSight(eyes.position, player.position, ignoreRoot: transform, targetRoot: player).</para>
		/// <para>It's a thin line, not a volume: gaps smaller than a sphere / capsule would fit through still count as visible.</para>
		/// <para>Colliders that contain the 'from' point are not detected (Unity raycasts don't hit colliders they start inside).</para>
		/// <para>Generates no garbage after the first call.</para>
		/// </summary>
		/// <param name="from">Start of the line (e.g. the viewer's eyes).</param>
		/// <param name="to">End of the line (e.g. the target's position).</param>
		/// <param name="ignoreRoot">Colliders on this transform and its children don't block (usually the viewer itself).</param>
		/// <param name="targetRoot">Colliders on this transform and its children don't block either (usually the target, so hitting it doesn't count as blocked).</param>
		/// <param name="collisionLayerMask">The layers that can block. All layers if null.</param>
		/// <param name="ignoreTriggers">Whether trigger colliders can block.</param>
		public static bool HasLineOfSight
		(
			Vector3 from,
			Vector3 to,
			Transform ignoreRoot = null,
			Transform targetRoot = null,
			LayerMask? collisionLayerMask = null,
			QueryTriggerInteraction ignoreTriggers = QueryTriggerInteraction.Ignore
		)
		{
			int mask = collisionLayerMask ?? Physics.AllLayers;

			// Nothing to filter out: a single linecast is enough (cheapest query, no buffer needed)
			if (ignoreRoot == null && targetRoot == null)
			{
				return !Physics.Linecast(from, to, mask, ignoreTriggers);
			}

			Vector3 delta = to - from;
			float distance = delta.magnitude;
			if (distance <= Mathf.Epsilon) return true;
			Vector3 direction = delta / distance;

			s_lineOfSightHits ??= new RaycastHit[16];
			int hits = Physics.RaycastNonAlloc(from, direction, s_lineOfSightHits, distance, mask, ignoreTriggers);

			// Hits are unordered and capped by the buffer, so a full buffer could hide a blocker: grow it and query again
			while (hits == s_lineOfSightHits.Length)
			{
				s_lineOfSightHits = new RaycastHit[s_lineOfSightHits.Length * 2];
				hits = Physics.RaycastNonAlloc(from, direction, s_lineOfSightHits, distance, mask, ignoreTriggers);
			}

			for (int h = 0; h < hits; h++)
			{
				Collider hit = s_lineOfSightHits[h].collider;
				if (IsIgnoredCollider(hit, ignoreRoot) || IsIgnoredCollider(hit, targetRoot)) continue;
				return false; // Something else is in the way.
			}
			return true;
		}

		private static bool IsIgnoredCollider(Collider c, Transform ignoreRoot) => ignoreRoot != null && c.transform.IsChildOf(ignoreRoot);
	}
}
