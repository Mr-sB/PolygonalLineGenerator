using System;
using System.Collections.Generic;
//using CustomizationInspector.Runtime;
using mattatz.Triangulation2DSystem;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
[ExecuteInEditMode]
public class MeshGenerator : MonoBehaviour
{
	[Serializable]
	public struct PointInfo
	{
		public Vector3 Point;
		public float Length;//距离起点的路径长度

		public PointInfo(Vector3 point, float length)
		{
			Point = point;
			Length = length;
		}
	}
	[Tooltip("横截面shape的旋转值")]
	[SerializeField] private float mRotateAngle;
	
	[Tooltip("最小锐角的阈值")]
	[SerializeField] private float mAngleThreshold = 10;
	
	[Tooltip("横截面的缩放")]
	[SerializeField] private Vector2 mScale = Vector2.one;
	
	[Tooltip("uv的缩放")]
	[SerializeField] private Vector2 mUVScale = Vector2.one;
	
	[Tooltip("是否自定义横截面shape,为false的话横截面为圆")]
	[SerializeField] private bool mCustomShapeVertices;
	
	[Tooltip("圆的半径")]
//	[HideIf(nameof(mCustomShapeVertices))]
	[SerializeField] private float mRadius = 0.5f;
	
	[Tooltip("横截面的顶点数")]
//	[HideIf(nameof(mCustomShapeVertices))]
	[SerializeField] private int mShapeVerticesLength = 4;
	
	[Tooltip("横截面顶点(顺时针)")]
//	[InfoBox(nameof(mCustomShapeVertices)+"为true时才编辑!")]
	[SerializeField] private Vector2[] mShapeVertices;
	
	[Header("横截面uv")]
	[Tooltip("是否自定义横截面uv,为false的话自动平铺计算")]
	[SerializeField] private bool mCustomShapeUVs;
	
	[Tooltip("横截面uv(顺时针),uv数量应该与mShapeVerticesLength相同")]
//	[InfoBox(nameof(mCustomShapeUVs)+"为true时才编辑!")]
	[SerializeField] private Vector2[] mShapeUVs;
	
	[Header("横截面边缘uv")]
	[Tooltip("是否自定义横截面边缘uv,为false的话自动平铺计算")]
	[SerializeField] private bool mCustomShapeEdgeUVs;
	
	[Tooltip("横截面顶点边缘的uv(顺时针),uv数量应该与mRealShapeVerticesLength相同")]
//	[InfoBox(nameof(mCustomShapeEdgeUVs)+"为true时才编辑!")]
	[SerializeField] private Vector2[] mShapeEdgeUVs;
	
	[Header("Debug edit")]
	[Tooltip("路径关键点")]
	public List<PointInfo> PointInfos = new List<PointInfo>();//所有关键点
	
	private Vector2[] mRealShapeVertices;//拉伸旋转之后真实的shape
	private Quaternion mRotation;//横截面shape的旋转值
	private List<Vector3> mVertices = new List<Vector3>();//所有顶点
	private List<Vector2> mUVs = new List<Vector2>();//所有uv
	private List<Vector3> mNormals = new List<Vector3>();//所有法线
	private List<int> mTriangles = new List<int>();//所有三角面
	private int[] mShapeTriangle;//横截面shape的三角面
	private MeshFilter m_MeshFilter;
	private Mesh mMmesh;
	private int mRealShapeVerticesLength;//真实的横截面顶点数。因为有一个uv缝，需要多复制一个顶点
	private float mUVFactor;//一米对应多少uv
	private Vector4 mShapeUVMinMax = Vector4.zero;//xMin,xMax,yMin,yMax
	[Header("Do not edit!")]
	[SerializeField] private float mLength;


	public float Length
	{
		private set => mLength = value;
		get => mLength;
	}


	private void Awake()
	{
		PointInfos.Clear();
		mMmesh = new Mesh {name = "GeneratedMesh"};
		m_MeshFilter = GetComponent<MeshFilter>();
		m_MeshFilter.sharedMesh = mMmesh;

		Clear();
		InitData();
	}

	[ContextMenu("InitData")]
//	[Button]
	public void InitData()
	{
		mRotation = Quaternion.Euler(0, 0, mRotateAngle);
		if (!mCustomShapeVertices)
		{
			mRealShapeVerticesLength = mShapeVerticesLength + 1;
			mShapeVertices = new Vector2[mShapeVerticesLength];
			mRealShapeVertices = new Vector2[mShapeVerticesLength];
			float degreeStep = 2 * Mathf.PI / mShapeVerticesLength;
			float degree = -Mathf.PI;
			
			for (int i = 0; i < mShapeVerticesLength; i++, degree -= degreeStep)
			{
				mShapeVertices[i] = new Vector2(mRadius * Mathf.Cos(degree), mRadius * Mathf.Sin(degree));
				mRealShapeVertices[i] = mRotation * Vector2.Scale(mShapeVertices[i], mScale);
			}
			//三角化横截面
			int count = mShapeVerticesLength - 2;
			mShapeTriangle = new int[3 * count];
			for (int i = 0; i < count; i++)
			{
				mShapeTriangle[i * 3] = 0;
				mShapeTriangle[i * 3 + 1] = i + 1;
				mShapeTriangle[i * 3 + 2] = i + 2;
			}
		}
		else
		{
			mShapeVerticesLength = mShapeVertices.Length;
			mRealShapeVerticesLength = mShapeVerticesLength + 1;
			mRealShapeVertices = new Vector2[mShapeVerticesLength];
			for (int i = 0; i < mShapeVerticesLength; i++)
				mRealShapeVertices[i] = mRotation * Vector2.Scale(mShapeVertices[i], mScale);
			//三角化自定义的横截面
			var polygon = Polygon2D.Contour(mRealShapeVertices);
			var triangulation = new Triangulation2D(polygon, 0);
			mShapeTriangle = triangulation.Triangles;
		}

		float shapeCircumference = 0;//横截面的周长
		for (int i = 0; i < mShapeVerticesLength; i++)
			shapeCircumference += Vector3.Distance(mRealShapeVertices[i], mRealShapeVertices[(i + 1) % mShapeVerticesLength]);
		mUVFactor = 1 / shapeCircumference;
		
		//计算边缘uv
		if (!mCustomShapeEdgeUVs)
		{
			mShapeEdgeUVs = new Vector2[mRealShapeVerticesLength];
			float uvX = 0;
			mShapeEdgeUVs[0] = Vector2.zero;
			for (int i = 0; i < mShapeVerticesLength; i++)
			{
				uvX += Vector3.Distance(mRealShapeVertices[(i + 1) % mShapeVerticesLength], mRealShapeVertices[i]) / shapeCircumference;
				mShapeEdgeUVs[i + 1] = new Vector2(uvX, 0);
			}
		}
		
		//计算底面uv
		if (!mCustomShapeUVs)
		{
			mShapeUVs = new Vector2[mShapeVerticesLength];
			Vector2 factor = new Vector2(mUVFactor, mUVFactor);
			for (int i = 0; i < mShapeVerticesLength; i++)
				mShapeUVs[i] = Vector2.Scale(mRealShapeVertices[i], factor);
			mShapeUVMinMax = new Vector4(mShapeUVs[0].x, mShapeUVs[0].x, mShapeUVs[0].y, mShapeUVs[0].y);
			for (int i = 0; i < mShapeVerticesLength; i++)
			{
				if (mShapeUVs[i].x < mShapeUVMinMax.x)//xMin
					mShapeUVMinMax.x = mShapeUVs[i].x;
				else if(mShapeUVs[i].x > mShapeUVMinMax.x)//xMax
					mShapeUVMinMax.y = mShapeUVs[i].x;
				
				if (mShapeUVs[i].y < mShapeUVMinMax.z)//yMin
					mShapeUVMinMax.z = mShapeUVs[i].y;
				else if(mShapeUVs[i].y > mShapeUVMinMax.w)//yMax
					mShapeUVMinMax.w = mShapeUVs[i].y;
			}
		}
	}

	public void Add(Vector3 point, bool refresh = true)
	{
		bool addExtra = AddPoint(point);

		if (PointInfos.Count < 2) return;
		
		if (PointInfos.Count > 2)
		{
			//去除最后一圈顶点
			mVertices.RemoveRange(mVertices.Count - (mRealShapeVerticesLength + mShapeVerticesLength), mRealShapeVerticesLength + mShapeVerticesLength);
			//两个三角面
			mTriangles.RemoveRange(mTriangles.Count - (mShapeVerticesLength - 2) * 3, (mShapeVerticesLength - 2) * 3);
			mUVs.RemoveRange(mUVs.Count - (mRealShapeVerticesLength + mShapeVerticesLength), mRealShapeVerticesLength + mShapeVerticesLength);
		}
		
		if (addExtra)
		{
			//计算最后三个点
			Generate(PointInfos.Count - 3);
			Generate(PointInfos.Count - 2);
			Generate(PointInfos.Count - 1);
		}
		else
		{
			//计算最后两个点
			Generate(PointInfos.Count - 2);
			Generate(PointInfos.Count - 1);
		}
		
		if (refresh)
			RefreshMesh();
	}

	public bool AddPoint(Vector3 point)
	{
		bool addExtra = false;
		if (PointInfos.Count > 1)
		{
			Vector3 v1 = PointInfos[PointInfos.Count - 2].Point - PointInfos[PointInfos.Count - 1].Point;
			Vector3 v2 = point - PointInfos[PointInfos.Count - 1].Point;
			float angle = Vector3.SignedAngle(v1, v2, Vector3.up);
			//拐角太锐，增加一个拐点
			if (Mathf.Abs(angle) <= mAngleThreshold)
			{
				addExtra = true;
				//往最新点的方向靠
				Vector3 moveDir = Vector3.Cross(v1 + v2, Vector3.up).normalized;
				if (angle > 0)
					moveDir = -moveDir;
				Vector3 p = PointInfos[PointInfos.Count - 1].Point + moveDir * (Vector3.Dot(moveDir, v2) * Mathf.Sin(Mathf.Deg2Rad * Mathf.Abs(angle)));
				AddPointInternal(p);
			}
		}
		AddPointInternal(point);
		return addExtra;
	}

	private void AddPointInternal(Vector3 point)
	{
		if (PointInfos.Count >= 1)
			Length += Vector3.Distance(PointInfos[PointInfos.Count - 1].Point, point);
		PointInfos.Add(new PointInfo(point, Length));
	}

	public void RefreshMesh()
	{
		mMmesh.SetVertices(mVertices);
		mMmesh.SetTriangles(mTriangles, 0);
		mMmesh.SetUVs(0, mUVs);
		//自动平滑法线
		mMmesh.RecalculateNormals();
		//uv分界处有两个顶点，自动平滑的法线会出现两个方向。手动合成为一个方向
		mMmesh.GetNormals(mNormals);
		for (int index = mShapeVerticesLength, count = mNormals.Count - mShapeVerticesLength; index < count; index += mRealShapeVerticesLength)
		{
			var normal = mNormals[index] + mNormals[index + mShapeVerticesLength];
			normal.Normalize();
			mNormals[index] = mNormals[index + mShapeVerticesLength] = normal;
		}
		mMmesh.SetNormals(mNormals);
	}
	
	[ContextMenu("GenerateAll")]
//	[Button]
	public void GenerateAll()
	{
		if(PointInfos.Count < 2) return;
		mVertices.Clear();
		mTriangles.Clear();
		mUVs.Clear();
		mNormals.Clear();
		for (int i = 0, count = PointInfos.Count; i < count; i++)
			Generate(i);
		RefreshMesh();
	}

	[ContextMenu("Clear")]
//	[Button]
	public void Clear()
	{
		PointInfos.Clear();
		mVertices.Clear();
		mTriangles.Clear();
		mUVs.Clear();
		mNormals.Clear();
		Length = 0;
		mMmesh.triangles = null;//避免顶点数不满足三角面数产生报错
		RefreshMesh();
	}
	
	private void Generate(int index)
	{
		if (index == 0)
		{
			//计算旋转
			Quaternion rot = Quaternion.LookRotation(PointInfos[1].Point - PointInfos[0].Point);
			//添加顶点
			AddForwardVerticesAndUVs(PointInfos[0], rot);
			AddMiddleVerticesAndUVs(PointInfos[0], rot);
			//添加三角面
			AddForwardPolygonTriangles();
		}
		else if (index == PointInfos.Count - 1)
		{
			//计算旋转
			Quaternion rot = Quaternion.LookRotation(PointInfos[index].Point - PointInfos[index - 1].Point);
			//添加顶点
			AddMiddleVerticesAndUVs(PointInfos[index], rot);
			AddBackVerticesAndUVs(PointInfos[index], rot);
			//添加三角面
			AddMiddlePolygonTriangles((index - 1) * mRealShapeVerticesLength + mShapeVerticesLength);
			AddBackPolygonTriangles((index + 1) * mRealShapeVerticesLength + mShapeVerticesLength);
		}
		else
		{
			//计算旋转
			var v1 = (PointInfos[index - 1].Point - PointInfos[index].Point).normalized;
			var v2 = (PointInfos[index + 1].Point - PointInfos[index].Point).normalized;
			//切线
			var vv = Vector3.Cross(v1 + v2, Vector3.up);
			float angle = Vector3.Angle(PointInfos[index].Point - PointInfos[index - 1].Point, vv);
			//防止旋转出错
			var value = Vector3.SignedAngle(PointInfos[index - 1].Point - PointInfos[index].Point, PointInfos[index].Point - PointInfos[index + 1].Point, Vector3.up);
			Quaternion rot;
			if(Mathf.Abs(value) <= 1)
				rot = Quaternion.LookRotation(PointInfos[index].Point - PointInfos[index - 1].Point);
			else
			{
				Vector3 form = value > 0 ? Vector3.forward : Vector3.back;
				rot = Quaternion.Euler(new Vector3(0, Vector3.SignedAngle(form, vv, Vector3.up), 0));
			}
			//添加顶点
			AddMiddleVerticesAndUVs(PointInfos[index], rot, angle);
			//添加三角面
			AddMiddlePolygonTriangles((index - 1) * mRealShapeVerticesLength + mShapeVerticesLength);
		}
	}

	private void AddForwardPolygonTriangles()
	{
		for (int i = 0, len = mShapeTriangle.Length; i < len; i++)
			mTriangles.Add(mShapeTriangle[i]);
	}
	
	private void AddBackPolygonTriangles(int offset)
	{
		for (int i = mShapeTriangle.Length - 1; i >= 0; i--)
			mTriangles.Add(offset + mShapeTriangle[i]);
	}

	private void AddMiddlePolygonTriangles(int offset)
	{
		for (int i = 0; i < mShapeVerticesLength; i++)
		{
			mTriangles.Add(offset + i); mTriangles.Add(offset + mRealShapeVerticesLength + i); mTriangles.Add(offset + i + 1);
			mTriangles.Add(offset + mRealShapeVerticesLength + i); mTriangles.Add(offset + mRealShapeVerticesLength + i + 1); mTriangles.Add(offset + i + 1);
		}
	}

	private void AddForwardVerticesAndUVs(PointInfo pointInfo, Quaternion rot)
	{
		for (int i = 0; i < mShapeVerticesLength; i++)
		{
			var vertex = mRealShapeVertices[i];
			mVertices.Add(pointInfo.Point + rot * vertex);
			
			var uv = mShapeUVs[i];
			//移动到第四象限 避免uv重合
			uv -= new Vector2(mShapeUVMinMax.x, mShapeUVMinMax.w);//xMin yMax
			uv.Scale(mUVScale);
			mUVs.Add(uv);
		}
	}
	
	private void AddBackVerticesAndUVs(PointInfo pointInfo, Quaternion rot)
	{
		for (int i = 0; i < mShapeVerticesLength; i++)
		{
			var vertex = mRealShapeVertices[i];
			mVertices.Add(pointInfo.Point + rot * vertex);
			
			var uv = mShapeUVs[i];
			uv -= new Vector2(mShapeUVMinMax.x, mShapeUVMinMax.w);//xMin yMax
			//垂直镜像到第一象限 避免uv重合
			uv.y = -uv.y;
			uv.y += pointInfo.Length * mUVFactor;
			uv.Scale(mUVScale);
			mUVs.Add(uv);
		}
	}
	
	private void AddMiddleVerticesAndUVs(PointInfo pointInfo, Quaternion rot, float? angle = null)
	{
		for (int i = 0; i < mRealShapeVerticesLength; i++)
		{
			var vertex = mRealShapeVertices[i % mShapeVerticesLength];
			//缩放顶点
			if (angle.HasValue)
			{
				var factor = Mathf.Abs(Mathf.Cos(Mathf.Deg2Rad * angle.Value));
				if (Mathf.Abs(factor) > 1e-4)
					vertex.x /= factor;
			}
			mVertices.Add(pointInfo.Point + rot * vertex);
			var uv = mShapeEdgeUVs[i];
			uv.y += pointInfo.Length * mUVFactor;
			uv.Scale(mUVScale);
			mUVs.Add(uv);
		}
	}
}