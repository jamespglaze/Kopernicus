/**
 * Kopernicus Planetary System Modifier
 * -------------------------------------------------------------
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this library; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston,
 * MA 02110-1301  USA
 *
 * This library is intended to be used as a plugin for Kerbal Space Program
 * which is copyright of TakeTwo Interactive. Your usage of Kerbal Space Program
 * itself is governed by the terms of its EULA, not the license above.
 *
 * https://kerbalspaceprogram.com
 */

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Kopernicus.ConfigParser;
using Kopernicus.Configuration;
using Kopernicus.Constants;
using UnityEngine;

namespace Kopernicus
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class IniTreeGeneration : MonoBehaviour
    {
        public Tree<CelestialBody> orbitHierarchy;
        public static Tree<CelestialBody> GetTree()
        {
            return GameObject.Find("OrbitTreeGenerationObject").GetComponent<IniTreeGeneration>().orbitHierarchy;
        }
        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            gameObject.name = "OrbitTreeGenerationObject";

            orbitHierarchy = new Tree<CelestialBody>();
            orbitHierarchy.root = new Node<CelestialBody>(FlightGlobals.Bodies[0]);
            List<Node<CelestialBody>> completeList = new List<Node<CelestialBody>>();
            for (int i = 0; i < FlightGlobals.Bodies.Count; i++)
                completeList[i] = new Node<CelestialBody>(FlightGlobals.Bodies[i]);
            foreach (Node<CelestialBody> node in completeList)
                node.SetParent(completeList.Find(a => a.item == node.item.orbit.referenceBody));
        }
    }
    public class Tree<T>
    {
        public Node<T> root;
        public List<Node<T>> elements
        {
            get
            {
                if (internalStorage == null)
                    internalStorage = new List<Node<T>>();
                else
                    internalStorage.Clear();
                AddNodesToList(root);
                return internalStorage;
            }
        }
        List<Node<T>> internalStorage;
        void AddNodesToList(Node<T> node)
        {
            internalStorage.Add(node);
            if (node.children != null && node.children.Count != 0)
                foreach (Node<T> n in node.children)
                    AddNodesToList(n);
        }
    }
    public class Node<T>
    {
        public List<Node<T>> children;
        public Node<T> parent;
        public T item;

        public Node(T i)
        {
            item = i;
            children = new List<Node<T>>();
        }
        public Node(T i, Node<T> parent)
        {
            item = i;
            parent.AddChildren(this);
            children = new List<Node<T>>();
        }

        public void AddChildren(Node<T> element)
        {
            if (children == null)
                children = new List<Node<T>>();
            children.Add(element);
            element.parent = this;
        }
        public void SetParent(Node<T> element)
        {
            if (parent != null)
                parent.RemoveChildren(this);
            element.AddChildren(this);
        }
        public void RemoveChildren(Node<T> element)
        {
            if (children == null)
                return;
            element.parent = null;
            children.Remove(element);
        }
    }
}
