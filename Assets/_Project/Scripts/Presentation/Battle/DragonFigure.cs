using UnityEngine;

namespace P2D.Presentation.Battle
{
    /// <summary>
    /// Builds a <see cref="DragonLook"/> from 3D primitives (greybox low-poly) under a parent, facing +Z.
    /// Holds the parts that animate (wing flap, orb bob) and the body renderer for hit flashes.
    /// </summary>
    public sealed class DragonFigure
    {
        public Transform Root { get; private set; }
        public Renderer Body { get; private set; }
        public DragonLook Look { get; private set; }

        Transform wingL;
        Transform wingR;
        Transform orb;
        Material baseMaterial;

        public static DragonFigure Build(Transform parent, DragonLook look, Material baseMaterial, bool withRider)
        {
            var f = new DragonFigure { Look = look, baseMaterial = baseMaterial };
            var root = new GameObject("Dragon Figure").transform;
            root.SetParent(parent, false);
            // The battle camera is close behind the dragon, so it grows by a quarter of the stage scale.
            root.localScale = Vector3.one * (0.75f + 0.25f * look.scale);
            f.Root = root;

            float w = look.bulk;
            Color accent = look.AccentShown;
            Color horn = look.glow ? accent : Color.Lerp(look.belly, Color.white, 0.2f);

            f.Body = f.Part(PrimitiveType.Capsule, root, "Body", new Vector3(0, 0.8f, 0), new Vector3(1.1f * w, 0.9f, 1.0f * w), look.body);
            f.Body.transform.localRotation = Quaternion.Euler(90, 0, 0);
            f.Part(PrimitiveType.Sphere, root, "Belly", new Vector3(0, 0.65f, 0.45f), new Vector3(0.9f * w, 0.7f, 0.8f), look.belly);

            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -0.45f : 0.45f) * w;
                float z = i < 2 ? 0.45f : -0.45f;
                f.Part(PrimitiveType.Cube, root, "Leg", new Vector3(x, 0.25f, z), new Vector3(0.28f, 0.5f, 0.28f), look.body * 0.8f);
                if (i < 2)
                {
                    var claw = f.Part(PrimitiveType.Cube, root, "Claw", new Vector3(x, 0.08f, z + 0.2f), new Vector3(0.18f, 0.12f, 0.3f) * look.clawSize, horn);
                    claw.transform.localRotation = Quaternion.Euler(20, 0, 0);
                }
            }

            var head = f.Part(PrimitiveType.Sphere, root, "Head", new Vector3(0, 1.35f, 1.1f), Vector3.one * 0.72f, look.body * 0.9f).transform;
            f.Part(PrimitiveType.Cube, root, "Snout", new Vector3(0, 1.25f, 1.5f), new Vector3(0.42f, 0.3f, 0.45f), look.body * 0.9f);
            f.Part(PrimitiveType.Sphere, root, "Eye L", new Vector3(-0.2f, 1.45f, 1.38f), Vector3.one * 0.13f, accent, true);
            f.Part(PrimitiveType.Sphere, root, "Eye R", new Vector3(0.2f, 1.45f, 1.38f), Vector3.one * 0.13f, accent, true);

            if (look.horns)
                for (int side = -1; side <= 1; side += 2)
                {
                    var h = f.Part(PrimitiveType.Cube, head, "Horn", new Vector3(0.28f * side, 0.55f, -0.25f), new Vector3(0.14f, 0.6f, 0.14f) * look.hornSize, horn, look.glow);
                    h.transform.localRotation = Quaternion.Euler(-35, 0, -15 * side);
                }
            if (look.crest)
            {
                var c = f.Part(PrimitiveType.Cube, root, "Crest", new Vector3(0, 1.78f, 1.0f), new Vector3(0.22f, 0.22f, 0.22f), accent, look.glow);
                c.transform.localRotation = Quaternion.Euler(45, 0, 45);
            }

            if (look.wings)
            {
                float len = 1.6f * look.wingLength;
                f.wingL = f.Part(PrimitiveType.Cube, root, "Wing L", new Vector3(-(0.4f * w + len / 2), 1.25f, -0.05f), new Vector3(len, 0.08f, 0.9f), look.wing).transform;
                f.wingR = f.Part(PrimitiveType.Cube, root, "Wing R", new Vector3(0.4f * w + len / 2, 1.25f, -0.05f), new Vector3(len, 0.08f, 0.9f), look.wing).transform;
                f.wingL.localRotation = Quaternion.Euler(0, 0, 20);
                f.wingR.localRotation = Quaternion.Euler(0, 0, -20);
            }

            if (look.tail)
            {
                var t1 = f.Part(PrimitiveType.Cube, root, "Tail", new Vector3(0, 0.6f, -1.2f), new Vector3(0.38f, 0.3f, 0.9f), look.body);
                t1.transform.localRotation = Quaternion.Euler(-12, 0, 0);
                var t2 = f.Part(PrimitiveType.Cube, root, "Tail Tip", new Vector3(0, 0.45f, -1.85f), new Vector3(0.25f, 0.2f, 0.7f), look.body * 0.85f);
                t2.transform.localRotation = Quaternion.Euler(-8, 0, 0);
                var tip = f.Part(PrimitiveType.Cube, root, "Tail Blade", new Vector3(0, 0.42f, -2.25f), new Vector3(0.32f, 0.08f, 0.32f), horn, look.glow);
                tip.transform.localRotation = Quaternion.Euler(0, 45, 0);
            }

            if (look.backPlate)
            {
                f.Part(PrimitiveType.Cube, root, "Back Plate", new Vector3(0, 1.32f, -0.05f), new Vector3(0.8f * w, 0.16f, 1.2f), accent, look.glow);
                for (int i = 0; i < 3; i++)
                    f.Part(PrimitiveType.Cube, root, "Plate Ridge", new Vector3(0, 1.46f, 0.35f - i * 0.4f), new Vector3(0.3f, 0.14f, 0.24f), accent * 0.85f);
            }

            if (look.orb)
            {
                f.orb = f.Part(PrimitiveType.Sphere, root, "Orb", new Vector3(1.3f, 2.1f, 0.5f), Vector3.one * (0.35f + 0.06f * look.tier), accent, true).transform;
            }

            if (withRider)
                f.Part(PrimitiveType.Cube, root, "Rider", new Vector3(0, 1.75f, -0.1f), new Vector3(0.35f, 0.5f, 0.35f), new Color(0.85f, 0.75f, 0.5f));
            return f;
        }

        /// <summary>Idle motion: wing flap and orb bob. Visual only.</summary>
        public void Animate(float time)
        {
            if (wingL != null)
            {
                float flap = 20 + Mathf.Sin(time * 4f) * 12f;
                wingL.localRotation = Quaternion.Euler(0, 0, flap);
                wingR.localRotation = Quaternion.Euler(0, 0, -flap);
            }
            if (orb != null) orb.localPosition = new Vector3(1.3f, 2.1f + Mathf.Sin(time * 2.5f) * 0.15f, 0.5f);
        }

        public void Destroy()
        {
            if (Root != null) Object.Destroy(Root.gameObject);
        }

        Renderer Part(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 scale, Color color, bool emissive = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            color.a = 1;
            var m = new Material(baseMaterial) { color = color };
            if (emissive && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * 0.35f);
            }
            r.material = m;
            return r;
        }
    }
}
