﻿using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using XmlRpc_Wrapper;
using gm = Messages.geometry_msgs;
using Messages.tf;
using tf.net;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Ros_CSharp;

public class TfVisualizer : MonoBehaviour
{
    private NodeHandle nh = null;
    private Subscriber<Messages.tf.tfMessage> tfsub, tfstaticsub;
    private Text textmaybe;
    private Queue<Messages.tf.tfMessage> transforms = new Queue<Messages.tf.tfMessage>();

    private volatile int lasthz = 0;
    private volatile int count = 0;
    private DateTime last = DateTime.Now;

    public Transform Template;
    public Transform Root;

    public string FixedFrame;
    public ROSManager ROSManager;

    private Dictionary<string, Transform> tree = new Dictionary<string, Transform>();

    private void hideChildrenInHierarchy(Transform trans)
    {
        for (int i = 0; i < trans.childCount; i++)
            trans.GetChild(i).hideFlags |= HideFlags.HideInHierarchy;
    }

    // Use this for initialization
	void Start ()
    {
        if (Template != null)
        {
            Template.gameObject.SetActive(false);
            hideChildrenInHierarchy(Template);
            Template.hideFlags |= HideFlags.HideAndDontSave;
        }
#if UNITY_EDITOR
        ObjectNames.SetNameSmart(Root, FixedFrame);
#endif
	    Root.GetComponentInChildren<TextMesh>().text = FixedFrame;
        tree[FixedFrame] = Root;
        hideChildrenInHierarchy(Root);

	    ROSManager.GetComponent<ROSManager>().StartROS(() =>
	                                                       {
	                                                           nh = new NodeHandle();
	                                                           tfstaticsub = nh.subscribe<Messages.tf.tfMessage>("/tf_static", 0, tf_callback, true);
	                                                           tfsub = nh.subscribe<Messages.tf.tfMessage>("/tf", 0, tf_callback, true);
	                                                       });
    }

    private void tf_callback(tfMessage msg)
    {
        lock (transforms)
        {
            transforms.Enqueue(msg);
            DateTime now = DateTime.Now;
            count++;
            if (now.Subtract(last).TotalMilliseconds > 1000)
            {
                lasthz = count;
                count = 0;
                last = now;
            }
        }
    }

    private bool IsVisible(string child_frame_id)
    {
        //TODO rviz style checkboxes?
        return true;
    }

    // Update is called once per frame
	void Update ()
	{
	    Queue<Messages.tf.tfMessage> tfs = null;
	    lock (transforms)
	    {
	        if (transforms.Count > 0)
	        {
	            tfs = new Queue<tfMessage>(transforms);
	            transforms.Clear();
	        }
	    }
	    while (tfs != null && tfs.Count > 0)
	    {
	        emTransform[] tfz = Array.ConvertAll<gm.TransformStamped, emTransform>(tfs.Dequeue().transforms, (a) => new emTransform(a));
            foreach (emTransform tf in tfz)
            {
                if (!tf.frame_id.StartsWith("/"))
                    tf.frame_id = "/" + tf.frame_id;
                if (!tf.child_frame_id.StartsWith("/"))
                    tf.child_frame_id = "/" + tf.child_frame_id;
                if (IsVisible(tf.child_frame_id))
                {
                    Vector3 pos = new Vector3((float)-tf.origin.x, (float)tf.origin.y, (float)tf.origin.z);
                    Quaternion rot = new Quaternion((float)(tf.basis.x/tf.basis.w), (float)(-tf.basis.y/tf.basis.w), (float)(-tf.basis.z/tf.basis.w), 1.0f);
                    if (!tree.ContainsKey(tf.child_frame_id))
                    {
                        Transform value1;
                        if (tree.TryGetValue(tf.frame_id, out value1))
                            Template.SetParent(value1);
                        else
                            Template.SetParent(Root.transform);

                        Transform newframe = (Transform)Instantiate(Template, Template.localPosition, Template.localRotation);
                        hideChildrenInHierarchy(newframe);
#if UNITY_EDITOR
                        ObjectNames.SetNameSmart(newframe, tf.child_frame_id);
#endif
                        tree[tf.child_frame_id] = newframe;
                        tree[tf.child_frame_id].gameObject.GetComponentInChildren<TextMesh>().text = tf.child_frame_id;
                    }

                    Transform value;
                    if (tree.TryGetValue(tf.frame_id, out value))
                    {
                        tree[tf.child_frame_id].SetParent(value, false);
                        tree[tf.child_frame_id].gameObject.SetActive(true);
                    }
                    else
                        tree[tf.child_frame_id].gameObject.SetActive(false);

                    tree[tf.child_frame_id].localPosition = pos;
                    tree[tf.child_frame_id].localRotation = rot;
                }
            }
        }
	}
}
