using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using TwinView;

public static class ChamberBuilder {
    const string Generated="Assets/Generated";
    static Material jade, stone, dark, brass, floor, guardian;
    static Mesh guardianMesh, gateMesh;
    static Material gateMat;
    static Transform buildRoot;

    [MenuItem("Twin View/Build connected chambers")]
    public static void BuildScene() {
        Directory.CreateDirectory(Generated);Directory.CreateDirectory("Assets/Scenes");
        AssetDatabase.Refresh();
        ImportGuardian();
        gateMesh=ImportGate();
        jade=Material("Jade",new Color(.23f,.39f,.38f),.1f,.35f);
        stone=Material("Limestone",new Color(.60f,.57f,.47f),0,.22f);
        dark=Material("Deep jade",new Color(.075f,.135f,.14f),.1f,.3f);
        brass=Material("Aged brass",new Color(.55f,.40f,.19f),.65f,.55f);
        floor=Material("Slate floor",new Color(.16f,.23f,.23f),.05f,.3f);
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.40f,.48f,.48f);
        RenderSettings.fog=true;RenderSettings.fogColor=new Color(.045f,.075f,.085f);RenderSettings.fogDensity=.018f;
        QualitySettings.antiAliasing=4;QualitySettings.shadowDistance=25;
        buildRoot=null;
        var background=CameraAt("Full screen background",new Vector3(0,20,-20),Vector3.zero);
        background.depth=-20;background.cullingMask=0;background.clearFlags=CameraClearFlags.SolidColor;background.backgroundColor=new Color(.035f,.06f,.065f);
        var main=CameraAt("Main Camera",new Vector3(5,4,-7),new Vector3(0,1.4f,0));
        main.tag="MainCamera";main.fieldOfView=42;main.backgroundColor=new Color(.045f,.075f,.085f);
        main.clearFlags=CameraClearFlags.SolidColor;main.gameObject.AddComponent<AudioListener>();
        var first=BuildRoom(0,Vector3.zero,main);
        var second=BuildRoom(1,new Vector3(0,0,9),main);
        var journey=new GameObject("Connected rooms").AddComponent<JourneyController>();
        journey.Rooms=new[]{first,second};journey.ViewCamera=main;
        first.Journey=second.Journey=journey;
        journey.Bridge=BuildBridge(journey);
        journey.Relay=BuildRelay(journey);
        var shell=new GameObject("Game shell (title and ending)").AddComponent<GameShell>();shell.Journey=journey;journey.Shell=shell;
        PlayerSettings.companyName="TwinView Workshop";PlayerSettings.productName="双面机关匣";
        PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.runInBackground=true;
        PlayerSettings.colorSpace=ColorSpace.Linear;
        PlayerSettings.SplashScreen.show=false;
        PlayerSettings.SplashScreen.showUnityLogo=false;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone,ScriptingImplementation.Mono2x);
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/FirstChamber.unity");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/FirstChamber.unity",true)};
        AssetDatabase.SaveAssets();
        Debug.Log("TWINVIEW_SCENE_BUILT");
    }
    static ChamberController BuildRoom(int roomIndex,Vector3 offset,Camera main) {
        var root=new GameObject(roomIndex==0?"Courtyard":"Borrowed shadow chamber");
        buildRoot=root.transform;
        Block("Foundation",new Vector3(0,-.15f,.3f),new Vector3(7.4f,.3f,7.4f),dark,root.transform);
        for(int x=-3;x<=3;x++)for(int z=-3;z<=3;z++)
            Block("Slate tile",new Vector3(x,.015f,z+.3f),new Vector3(.98f,.06f,.98f),floor,root.transform);
        for(int i=-3;i<=3;i++) {
            Block("Border",new Vector3(i,.1f,-3.25f),new Vector3(.95f,.16f,.2f),stone,root.transform);
            Block("Border",new Vector3(-3.35f,.1f,i+.3f),new Vector3(.2f,.16f,.95f),stone,root.transform);
            Block("Border",new Vector3(3.35f,.1f,i+.3f),new Vector3(.2f,.16f,.95f),stone,root.transform);
        }
        Block("Back wall L",new Vector3(-2.35f,1.6f,3.4f),new Vector3(2.4f,3.2f,.35f),dark,root.transform);
        Block("Back wall R",new Vector3(2.35f,1.6f,3.4f),new Vector3(2.4f,3.2f,.35f),dark,root.transform);
        Block("Header",new Vector3(0,3.2f,3.4f),new Vector3(7.1f,.3f,.4f),stone,root.transform);
        for(int sign=-1;sign<=1;sign+=2) {
            Block("Door pillar",new Vector3(sign*1.25f,1.5f,3.15f),new Vector3(.32f,3,.55f),stone,root.transform);
            Block("Pillar inlay",new Vector3(sign*1.25f,1.5f,2.86f),new Vector3(.045f,2.75f,.025f),brass,root.transform);
            Block("Front pillar",new Vector3(sign*3.25f,.42f,-2.8f),new Vector3(.3f,.84f,.3f),jade,root.transform);
            Block("Pillar cap",new Vector3(sign*3.25f,.88f,-2.8f),new Vector3(.4f,.08f,.4f),brass,root.transform);
        }
        // A small illuminated passage gives the opened door a visible destination.
        Block("Beyond floor",new Vector3(0,-.02f,5),new Vector3(2.4f,.1f,3),stone,root.transform);
        if(roomIndex==1) Block("Beyond back",new Vector3(0,1.5f,6.5f),new Vector3(2.4f,3,.2f),jade,root.transform);
        else {
            for(int sign=-1;sign<=1;sign+=2)
                Block("Corridor rail",new Vector3(sign*1.15f,.35f,4.6f),new Vector3(.1f,.7f,2.5f),brass,root.transform);
        }
        Transform left=Block("Left door",new Vector3(-.55f,1.4f,3.25f),new Vector3(1.08f,2.8f,.18f),jade,root.transform).transform;
        Transform right=Block("Right door",new Vector3(.55f,1.4f,3.25f),new Vector3(1.08f,2.8f,.18f),jade,root.transform).transform;
        DoorDecoration(left);DoorDecoration(right);
        Cylinder("Plinth lower",new Vector3(0,.18f,0),new Vector3(2.35f,.18f,2.35f),dark,root.transform);
        Cylinder("Plinth brass",new Vector3(0,.4f,0),new Vector3(2.12f,.045f,2.12f),brass,root.transform);
        Cylinder("Turntable",new Vector3(0,.635f,0),new Vector3(1.92f,.19f,1.92f),jade,root.transform);
        Cylinder("Top lip",new Vector3(0,.845f,0),new Vector3(2.02f,.02f,2.02f),brass,root.transform);
        for(int i=0;i<12;i++) {
            float a=i*Mathf.PI/6;
            var marker=Block("Turn tick",new Vector3(Mathf.Sin(a)*1.08f,.45f,Mathf.Cos(a)*1.08f),new Vector3(.035f,.04f,.14f),stone,root.transform);
            marker.transform.rotation=Quaternion.Euler(0,i*30,0);
        }
        var artifact=new GameObject("Folded ridge guardian — Aholo 5792442");artifact.layer=8+roomIndex;
        artifact.transform.SetParent(root.transform);artifact.transform.position=new Vector3(0,1.65f,0);
        artifact.transform.localScale=Vector3.one*5;
        artifact.AddComponent<MeshFilter>().sharedMesh=guardianMesh;
        artifact.AddComponent<MeshRenderer>().sharedMaterial=guardian;
        var front=CameraAt("Front orthographic sensor",new Vector3(0,1.65f,-6),new Vector3(0,1.65f,0));
        var side=CameraAt("Side orthographic sensor",new Vector3(6,1.65f,0),new Vector3(0,1.65f,0));
        front.enabled=side.enabled=false;
        var screen1=Screen("Room "+roomIndex+" front seal",new Vector3(-2.4f,1.85f,3.16f),Quaternion.identity);
        var screen2=Screen("Room "+roomIndex+" side seal",new Vector3(2.4f,1.85f,3.16f),Quaternion.identity);
        var key=new GameObject("Warm key").AddComponent<Light>();key.transform.SetParent(root.transform);key.type=LightType.Directional;
        key.transform.rotation=Quaternion.Euler(42,-32,0);key.color=new Color(1,.91f,.74f);key.intensity=roomIndex==0?1.1f:0;key.shadows=LightShadows.Soft;
        var fill=Point("Soft jade fill",new Vector3(3,3,-2),new Color(.50f,.75f,.79f),2.5f,8);
        Point("Doorway glow",new Vector3(0,2.5f,4.5f),new Color(1,.75f,.35f),3,5);
        var ctrl=new GameObject("Chamber logic").AddComponent<ChamberController>();
        ctrl.transform.SetParent(root.transform);ctrl.RoomIndex=roomIndex;ctrl.ActiveRoom=roomIndex==0;
        ctrl.Artifact=artifact.transform;ctrl.DoorLeft=left;ctrl.DoorRight=right;ctrl.FrontCamera=front;ctrl.SideCamera=side;ctrl.ViewCamera=main;
        ctrl.FrontScreen=screen1;ctrl.SideScreen=screen2;ctrl.MaskShader=Shader.Find("Hidden/TwinView/ArtifactMask");
        ctrl.FrontLamp=Point("Front seal lamp",new Vector3(-2.4f,2.6f,2.8f),Color.yellow,1,2);
        ctrl.SideLamp=Point("Side seal lamp",new Vector3(2.4f,2.6f,2.8f),Color.yellow,1,2);
        if(roomIndex==0) {
            // Selection feedback stays outside every projection layer.
            const string glowPath=Generated+"/Selection overlay.mat";
            var glow=AssetDatabase.LoadAssetAtPath<Material>(glowPath);
            if(!glow) {glow=new Material(Shader.Find("TwinView/SelectionOverlay"));AssetDatabase.CreateAsset(glow,glowPath);}
            glow.shader=Shader.Find("TwinView/SelectionOverlay");glow.SetColor("_Color",new Color(.78f,.97f,.95f,.30f));EditorUtility.SetDirty(glow);
            var marker=new GameObject("Selection marker").transform;marker.SetParent(root.transform);
            var rail=Block("Selection rail",Vector3.zero,Vector3.one,glow,marker);
            var ring=Block("Selection slot bar",Vector3.zero,Vector3.one,glow,marker);
            foreach(var r in marker.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(r);
            var dots=new Transform[3];
            for(int i=0;i<3;i++) {var dot=Cylinder("Selection stop "+i,Vector3.zero,new Vector3(.1f,.05f,.1f),glow,marker);UnityEngine.Object.DestroyImmediate(dot.GetComponent<Collider>());dots[i]=dot.transform;}
            ctrl.MarkerDots=dots;
            var depthDots=new Transform[3];
            for(int i=0;i<3;i++) {var dot=Cylinder("Depth stop "+i,Vector3.zero,new Vector3(.1f,.05f,.1f),glow,marker);UnityEngine.Object.DestroyImmediate(dot.GetComponent<Collider>());depthDots[i]=dot.transform;dot.SetActive(false);}
            ctrl.DepthMarker=depthDots;
            ctrl.Marker=marker;ctrl.MarkerRail=rail.transform;ctrl.MarkerRing=ring.transform;
            marker.gameObject.SetActive(false);
        }
        if(roomIndex==0) {ctrl.Offset=BuildOffsetSeal(root.transform,ctrl);ctrl.Gate=BuildGateSeal(root.transform,ctrl);}
        if(roomIndex==1) {ctrl.Pillar=BuildPillar(root.transform);ctrl.PillarAlcove=BuildPillarAlcove(root.transform,ctrl);}
        else ctrl.Alcove=BuildAlcove(root.transform,ctrl);
        foreach(var renderer in root.GetComponentsInChildren<Renderer>())
            if(renderer.gameObject.layer==0) renderer.gameObject.layer=10+roomIndex;
        root.transform.position=offset;
        buildRoot=null;
        return ctrl;
    }
    // Offset seal: guardian only (layer 8); its depth rail is the ability lock.
    static OffsetSeal BuildOffsetSeal(Transform room,ChamberController owner) {
        var seal=new GameObject("Offset seal - depth ability lock").AddComponent<OffsetSeal>();seal.transform.SetParent(room);
        seal.Owner=owner;
        seal.FrontCamera=CameraAt("Offset front sensor",new Vector3(0,1.65f,-6),new Vector3(0,1.65f,0));
        seal.SideCamera=CameraAt("Offset side sensor",new Vector3(6,1.65f,0),new Vector3(0,1.65f,0));
        seal.FrontCamera.enabled=seal.SideCamera.enabled=false;
        // Depth rail marks, visible from the start.
        for(int d=0;d<3;d++) Block("Depth rail mark",new Vector3(-1.95f,.86f,(1-d)*.45f),new Vector3(.22f,.025f,.05f),brass,room);
        Block("Depth rail",new Vector3(-1.95f,.845f,0),new Vector3(.05f,.02f,1.2f),dark,room);
        return seal;
    }
    // Gate stone on layer 14: only this station's cameras see it.
    static GateSeal BuildGateSeal(Transform room,ChamberController owner) {
        var stone=new GameObject("Gate stone - Aholo 8151968");stone.layer=14;
        stone.transform.SetParent(room);stone.transform.localPosition=new Vector3(-1.05f,1.72f,1.95f);
        stone.transform.localRotation=Quaternion.Euler(0,0,0);stone.transform.localScale=Vector3.one*6.2f;
        stone.AddComponent<MeshFilter>().sharedMesh=gateMesh;
        stone.AddComponent<MeshRenderer>().sharedMaterial=gateMat;
        Block("Gate stone base",new Vector3(-1.05f,.32f,1.95f),new Vector3(1.9f,.55f,1.3f),dark,room);
        var seal=new GameObject("Gate seal - two objects, one window pair").AddComponent<GateSeal>();
        seal.transform.SetParent(room);seal.Owner=owner;seal.Gate=stone.transform;
        seal.FrontCamera=CameraAt("Gate front sensor",new Vector3(-.5f,1.65f,-6),new Vector3(-.5f,1.65f,0));
        seal.SideCamera=CameraAt("Gate side sensor",new Vector3(6,1.65f,.9f),new Vector3(0,1.65f,.9f));
        seal.FrontCamera.enabled=seal.SideCamera.enabled=false;
        return seal;
    }
    // Second knowledge lock in the borrow room.
    static PillarAlcove BuildPillarAlcove(Transform room, ChamberController owner) {
        var alcove = new GameObject("Pillar alcove - knowledge lock").AddComponent<PillarAlcove>();
        alcove.transform.SetParent(room); alcove.Owner = owner;
        var lid = Block("Pillar alcove lid", new Vector3(-2.45f, .62f, .55f), new Vector3(.9f, .1f, .7f), stone, room);
        var token = new GameObject("Pillar alcove token"); token.transform.SetParent(room);
        token.transform.localPosition = new Vector3(-2.45f, .5f, .55f);
        token.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Generated + "/StarToken.asset");
        token.AddComponent<MeshRenderer>().sharedMaterial = brass;
        alcove.Lid = lid.transform; alcove.Token = token.transform;
        alcove.FrontCamera = CameraAt("Pillar alcove front sensor", new Vector3(0, 1.65f, -6), new Vector3(0, 1.65f, 0));
        alcove.SideCamera = CameraAt("Pillar alcove side sensor", new Vector3(6, 1.65f, 0), new Vector3(0, 1.65f, 0));
        alcove.FrontCamera.enabled = alcove.SideCamera.enabled = false;
        return alcove;
    }
    static Transform BuildPillar(Transform room) {
        // Track hardware is outside the silhouette set. All carved stone parts participate.
        var pillar=new GameObject("Carved sliding pillar").transform;pillar.SetParent(room);
        Block("Pillar foot",new Vector3(0,.93f,0),new Vector3(.55f,.14f,.42f),jade,pillar);
        Block("Carved shaft",new Vector3(0,1.73f,0),new Vector3(.38f,1.46f,.34f),jade,pillar);
        // Cantilever links the two silhouettes without touching the guardian.
        Block("Cantilever crown",new Vector3(0,2.54f,-.45f),new Vector3(.55f,.16f,1.3f),stone,pillar);
        Block("Crown top",new Vector3(0,2.68f,0),new Vector3(.30f,.12f,.34f),jade,pillar);
        foreach(Transform part in pillar) part.gameObject.layer=9;
        Block("Raised track support",new Vector3(-.3f,.43f,1.28f),new Vector3(2.55f,.85f,.5f),dark,room);
        for(int sign=-1;sign<=1;sign+=2)
            Block("Pillar rail",new Vector3(-.3f,.87f,1.28f+sign*.24f),new Vector3(2.6f,.035f,.035f),brass,room);
        for(int slot=0;slot<3;slot++) {
            Block("Track stop",new Vector3(-1.2f+slot*.9f,.9f,1.56f),new Vector3(.14f,.06f,.12f),stone,room);
            for(int tick=0;tick<=slot;tick++)
                Block("Track number",new Vector3(-1.2f+slot*.9f+(tick-slot*.5f)*.08f,.88f,1.02f),new Vector3(.035f,.025f,.09f),brass,room);
        }
        // Shared projection footprint; supports use a different material.
        for(int sign=-1;sign<=1;sign+=2) {
            Block("Projection boundary",new Vector3(sign*1.65f,.08f,0),new Vector3(.035f,.025f,3.3f),brass,room);
            Block("Projection boundary",new Vector3(0,.08f,sign*1.65f),new Vector3(3.3f,.025f,.035f),brass,room);
        }
        pillar.localPosition=new Vector3(-1.2f,0,1.28f);
        return pillar;
    }
    static SecretAlcove BuildAlcove(Transform room,ChamberController owner) {
        Block("Socket platform",new Vector3(.4f,.72f,0),new Vector3(4.4f,.22f,2.1f),dark,room);
        for(int i=0;i<3;i++) {
            float x=(i-1)*.9f;
            Block("Socket brass mark",new Vector3(x,.845f,-.95f),new Vector3(.52f,.03f,.045f),brass,room);
            for(int j=0;j<=i;j++) Block("Socket index",new Vector3(x+(j-i*.5f)*.1f,.85f,-1.02f),new Vector3(.04f,.02f,.09f),stone,room);
        }
        var frame=new GameObject("Fixed carved alcove frame").transform;frame.SetParent(room);
        Block("Carved frame upright",new Vector3(1.65f,1.7f,1.27f),new Vector3(.32f,1.7f,.32f),jade,frame);
        Block("Carved frame foot",new Vector3(1.65f,.94f,1.27f),new Vector3(.48f,.16f,.48f),stone,frame);
        Block("Carved frame overhang",new Vector3(1.15f,2.54f,.78f),new Vector3(1.3f,.16f,1.3f),stone,frame);
        Block("Carved frame crown",new Vector3(1.65f,2.7f,1.27f),new Vector3(.4f,.16f,.4f),jade,frame);
        foreach(Transform part in frame) part.gameObject.layer=12;
        Block("Alcove back",new Vector3(2.5f,.8f,1.05f),new Vector3(1.3f,1.5f,.12f),dark,room);
        for(int sign=-1;sign<=1;sign+=2)
            Block("Alcove side",new Vector3(2.5f+sign*.66f,.8f,.73f),new Vector3(.1f,1.5f,.75f),stone,room);
        Block("Alcove lintel",new Vector3(2.5f,1.55f,.73f),new Vector3(1.4f,.12f,.75f),brass,room);
        Block("Alcove sill",new Vector3(2.5f,.08f,.73f),new Vector3(1.4f,.12f,.75f),brass,room);
        var face=GameObject.CreatePrimitive(PrimitiveType.Quad).GetComponent<Renderer>();face.name="Alcove sliding cover";
        face.transform.SetParent(room);face.transform.localPosition=new Vector3(2.5f,.8f,.35f);face.transform.localScale=Vector3.one*1.2f;
        string facePath=Generated+"/Alcove display.mat";var faceMat=AssetDatabase.LoadAssetAtPath<Material>(facePath);
        if(!faceMat) {faceMat=new Material(Shader.Find("Unlit/Texture"));AssetDatabase.CreateAsset(faceMat,facePath);}
        face.sharedMaterial=faceMat;
        var star=new GameObject("Star inscription");star.transform.SetParent(room);star.transform.localPosition=new Vector3(2.5f,.65f,.75f);
        var mesh=new Mesh();mesh.name="Four point star";
        var vertices=new Vector3[10];vertices[0]=new Vector3(0,0,-.09f);vertices[9]=new Vector3(0,0,.09f);
        for(int i=0;i<8;i++) {float a=i*Mathf.PI/4;float r=i%2==0?.27f:.11f;vertices[i+1]=new Vector3(Mathf.Sin(a)*r,Mathf.Cos(a)*r,0);}
        var triangles=new int[48];
        for(int i=0;i<8;i++) {int next=(i+1)%8+1;triangles[i*6]=0;triangles[i*6+1]=i+1;triangles[i*6+2]=next;triangles[i*6+3]=9;triangles[i*6+4]=next;triangles[i*6+5]=i+1;}
        mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
        string path=Generated+"/StarToken.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(saved) {EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);mesh=saved;} else AssetDatabase.CreateAsset(mesh,path);
        star.AddComponent<MeshFilter>().sharedMesh=mesh;star.AddComponent<MeshRenderer>().sharedMaterial=brass;
        Point("Alcove warm light",new Vector3(2.5f,1.3f,.5f),new Color(1,.76f,.35f),1.3f,2);
        var secret=new GameObject("Knowledge lock - no progress gate").AddComponent<SecretAlcove>();secret.transform.SetParent(room);
        secret.Owner=owner;secret.Frame=frame;secret.Face=face;secret.Lid=face.transform;secret.Star=star.transform;
        secret.FrontCamera=CameraAt("Alcove front sensor",new Vector3(.45f,1.65f,-6),new Vector3(.45f,1.65f,0));
        secret.SideCamera=CameraAt("Alcove side sensor",new Vector3(6,1.65f,.3f),new Vector3(0,1.65f,.3f));
        secret.FrontCamera.enabled=secret.SideCamera.enabled=false;
        return secret;
    }
    static BridgePiece BuildBridge(JourneyController journey) {
        var piece=new GameObject("Portable notched bridge").AddComponent<BridgePiece>();piece.Journey=journey;
        piece.Shape=new GameObject("Bridge geometry").transform;
        Vector3 pivot=new Vector3(1.15f,1.65f,1.23f);
        Material bridgeStone=Material("Bridge blue limestone",new Color(.34f,.48f,.51f),.12f,.35f);
        Block("Bridge far leg",new Vector3(1.65f,1.7f,1.72f)-pivot,new Vector3(.32f,1.7f,.32f),bridgeStone,piece.Shape);
        Block("Bridge far foot",new Vector3(1.65f,.94f,1.72f)-pivot,new Vector3(.48f,.16f,.48f),bridgeStone,piece.Shape);
        Block("Bridge roof",new Vector3(1.15f,2.54f,1.23f)-pivot,new Vector3(1.3f,.16f,1.3f),bridgeStone,piece.Shape);
        Block("Bridge keystone",new Vector3(1.65f,2.7f,1.72f)-pivot,new Vector3(.4f,.16f,.4f),stone,piece.Shape);
        // Additional support is behind the guardian (z >= 1.63), never inside its mesh.
        Block("Bridge near leg",new Vector3(.65f,1.7f,1.72f)-pivot,new Vector3(.18f,1.52f,.18f),bridgeStone,piece.Shape);
        Block("Bridge display plinth",new Vector3(-2.6f,.45f,8),new Vector3(.85f,.9f,.85f),dark,journey.Rooms[1].transform.parent).layer=11;
        piece.Apply(false,0,1);return piece;
    }
    static RoomRelay BuildRelay(JourneyController journey) {
        var relay=new GameObject("Cross-room brass transmission").AddComponent<RoomRelay>();relay.Journey=journey;
        Transform room=journey.Rooms[1].transform.parent;Vector3 origin=room.position;
        var wheel=new GameObject("Three-setting hand wheel").transform;wheel.SetParent(room);wheel.localPosition=new Vector3(2.55f,1.2f,-1);
        var disc=Cylinder("Brass wheel",wheel.position,new Vector3(.78f,.07f,.78f),brass,wheel);disc.transform.localRotation=Quaternion.Euler(90,0,0);
        for(int i=0;i<12;i++) {
            float a=i*Mathf.PI/6;
            var tooth=Block("Gear tooth",wheel.position+new Vector3(Mathf.Sin(a)*.43f,Mathf.Cos(a)*.43f,0),new Vector3(.12f,.16f,.14f),brass,wheel);
            tooth.transform.localRotation=Quaternion.Euler(0,0,-i*30);
        }
        Block("Wheel pointer",wheel.position+new Vector3(0,.26f,-.1f),new Vector3(.06f,.32f,.05f),stone,wheel);
        Block("Transmission pedestal",origin+new Vector3(2.55f,.4f,-.9f),new Vector3(.7f,.8f,.6f),dark,room);
        foreach(var renderer in wheel.GetComponentsInChildren<Renderer>()) renderer.gameObject.layer=11;
        relay.Wheel=wheel;
        Vector3[] route={new Vector3(1.65f,2.45f,1.27f),new Vector3(3.05f,2.45f,1.27f),new Vector3(3.05f,.25f,1.27f),new Vector3(3.05f,.25f,8),new Vector3(2.55f,.25f,8),new Vector3(2.55f,1.2f,8)};
        for(int i=0;i<route.Length-1;i++) {
            var shaft=Block("Brass connecting shaft",(route[i]+route[i+1])*.5f,new Vector3(.075f,.075f,Vector3.Distance(route[i],route[i+1])),brass,null);
            shaft.transform.rotation=Quaternion.LookRotation(route[i+1]-route[i]);
        }
        Transform courtyard=journey.Rooms[0].transform.parent;
        Block("Final seal backplate",new Vector3(0,2.75f,3.02f),new Vector3(1.25f,.82f,.1f),brass,courtyard).layer=10;
        relay.SealLeft=Block("Final seal left leaf",new Vector3(-.29f,2.75f,2.87f),new Vector3(.56f,.72f,.12f),jade,courtyard).transform;
        relay.SealRight=Block("Final seal right leaf",new Vector3(.29f,2.75f,2.87f),new Vector3(.56f,.72f,.12f),jade,courtyard).transform;
        relay.SealLeft.gameObject.layer=relay.SealRight.gameObject.layer=10;
        var prize=new GameObject("Final star");prize.layer=10;prize.transform.SetParent(courtyard);prize.transform.localPosition=new Vector3(0,2.75f,2.96f);
        prize.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Generated+"/StarToken.asset");prize.AddComponent<MeshRenderer>().sharedMaterial=stone;
        relay.Prize=prize.transform;
        relay.PreviewCamera=CameraAt("Live courtyard relay view",new Vector3(5,5,-7),new Vector3(.6f,1.5f,.3f));
        relay.PreviewCamera.enabled=false;relay.PreviewCamera.fieldOfView=45;
        relay.PreviewCamera.clearFlags=CameraClearFlags.SolidColor;relay.PreviewCamera.backgroundColor=new Color(.045f,.075f,.085f);
        return relay;
    }
    static Mesh ImportGate() {
        string path="Assets/Art/Gate/Gate.bytes";
        byte[] raw=File.ReadAllBytes(path);Mesh mesh;
        using(var reader=new BinaryReader(new MemoryStream(raw))) {
            int nv=reader.ReadInt32(),ni=reader.ReadInt32();var v=new Vector3[nv];var n=new Vector3[nv];var uv=new Vector2[nv];var index=new int[ni];
            for(int i=0;i<nv;i++)v[i]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
            for(int i=0;i<nv;i++)n[i]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
            for(int i=0;i<nv;i++)uv[i]=new Vector2(reader.ReadSingle(),reader.ReadSingle());
            for(int i=0;i<ni;i++)index[i]=reader.ReadInt32();
            mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Generated+"/Gate.asset");
            if(!mesh) {mesh=new Mesh();AssetDatabase.CreateAsset(mesh,Generated+"/Gate.asset");}
            mesh.indexFormat=nv>65000?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.Clear();mesh.name="Lux3D Gate stone";mesh.vertices=v;mesh.normals=n;mesh.uv=uv;mesh.triangles=index;
            mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
        }
        gateMat=Material("Gate stone",Color.white,.1f,.35f);
        gateMat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Gate/GateColor.png");
        var metallic=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Gate/GateMetallic.png");
        if(metallic) {var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/Art/Gate/GateMetallic.png");importer.sRGBTexture=false;importer.SaveAndReimport();
            gateMat.SetTexture("_MetallicGlossMap",metallic);gateMat.EnableKeyword("_METALLICGLOSSMAP");gateMat.SetFloat("_GlossMapScale",.7f);}
        EditorUtility.SetDirty(gateMat);
        return mesh;
    }
    static void ImportGuardian() {
        string path="Assets/Art/Guardian/Guardian.bytes";
        byte[] raw=File.ReadAllBytes(path);
        using(var reader=new BinaryReader(new MemoryStream(raw))) {
            int nv=reader.ReadInt32(),ni=reader.ReadInt32();var v=new Vector3[nv];var n=new Vector3[nv];var uv=new Vector2[nv];var index=new int[ni];
            for(int i=0;i<nv;i++)v[i]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
            for(int i=0;i<nv;i++)n[i]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
            for(int i=0;i<nv;i++)uv[i]=new Vector2(reader.ReadSingle(),reader.ReadSingle());
            for(int i=0;i<ni;i++)index[i]=reader.ReadInt32();
            guardianMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Generated+"/Guardian.asset");
            if(!guardianMesh) { guardianMesh=new Mesh();AssetDatabase.CreateAsset(guardianMesh,Generated+"/Guardian.asset"); }
            guardianMesh.Clear();guardianMesh.name="Lux3D Guardian";guardianMesh.vertices=v;guardianMesh.normals=n;guardianMesh.uv=uv;guardianMesh.triangles=index;
            guardianMesh.RecalculateBounds();guardianMesh.RecalculateTangents();EditorUtility.SetDirty(guardianMesh);
        }
        guardian=Material("Guardian",Color.white,.1f,.35f);
        guardian.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Guardian/GuardianColor.png");
        var metallic=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Guardian/GuardianMetallic.png");
        if(metallic) {
            var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/Art/Guardian/GuardianMetallic.png");
            importer.sRGBTexture=false;importer.SaveAndReimport();
            guardian.SetTexture("_MetallicGlossMap",metallic);guardian.EnableKeyword("_METALLICGLOSSMAP");guardian.SetFloat("_GlossMapScale",.7f);
        }
        EditorUtility.SetDirty(guardian);
    }
    static Material Material(string name,Color color,float metal,float smooth) {
        string path=Generated+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!mat) {mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,path);}
        mat.color=color;mat.SetFloat("_Metallic",metal);mat.SetFloat("_Glossiness",smooth);EditorUtility.SetDirty(mat);return mat;
    }
    static GameObject Block(string name,Vector3 pos,Vector3 size,Material mat,Transform parent) {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent?parent:buildRoot);go.transform.position=pos;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;return go;
    }
    static GameObject Cylinder(string name,Vector3 pos,Vector3 size,Material mat,Transform parent) {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(parent?parent:buildRoot);go.transform.position=pos;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;return go;
    }
    static void DoorDecoration(Transform door) {
        var trim=Block("Brass inset",Vector3.zero,Vector3.one,brass,door);trim.transform.localPosition=new Vector3(0,0,-.6f);trim.transform.localScale=new Vector3(.06f,.87f,.06f);
    }
    static Renderer Screen(string name,Vector3 pos,Quaternion rotation) {
        var frame=Block(name+" frame",pos,new Vector3(1.43f,1.43f,.12f),brass,null);
        var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.name=name+" projection";quad.transform.SetParent(buildRoot);quad.transform.position=pos+new Vector3(0,0,-.07f);
        quad.transform.localScale=Vector3.one*1.28f;quad.transform.rotation=rotation;
        string materialPath=Generated+"/"+name+" display.mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(!mat) {mat=new Material(Shader.Find("Unlit/Texture"));mat.name=name+" display";AssetDatabase.CreateAsset(mat,materialPath);}
        quad.GetComponent<Renderer>().sharedMaterial=mat;return quad.GetComponent<Renderer>();
    }
    static Camera CameraAt(string name,Vector3 position,Vector3 target) {var c=new GameObject(name).AddComponent<Camera>();c.transform.SetParent(buildRoot);c.transform.position=position;c.transform.LookAt(target);c.nearClipPlane=.1f;c.farClipPlane=30;return c;}
    static Light Point(string name,Vector3 pos,Color color,float strength,float range) {var l=new GameObject(name).AddComponent<Light>();l.transform.SetParent(buildRoot);l.type=LightType.Point;l.transform.position=pos;l.color=color;l.intensity=strength;l.range=range;return l;}

    static void ValidateBorrowRoom(ChamberController room,List<string> checks) {
        room.Initialize();int solutions=0;
        for(int pose=0;pose<4;pose++) for(int slot=0;slot<3;slot++) {
            room.SetStateImmediate(pose,slot);
            bool passed=room.Front.Passed&&room.Side.Passed;
            checks.Add("borrow pose="+pose+",slot="+slot+",front="+room.Front.Passed+",side="+room.Side.Passed);
            if(passed) solutions++;
            if(pose==0&&slot==2&&!passed) throw new Exception("Borrow room goal does not solve both windows");
        }
        if(solutions!=1) throw new Exception("Expected one introductory borrow solution, found "+solutions);
        room.Pillar.gameObject.SetActive(false);
        for(int pose=0;pose<4;pose++) {
            room.SetStateImmediate(pose,0);
            if(room.Front.Passed&&room.Side.Passed) throw new Exception("Guardian alone bypasses the borrowed-shadow rule");
        }
        checks.Add("borrow guardian alone rejected in all four poses");
        room.Pillar.gameObject.SetActive(true);
        room.Artifact.GetComponent<Renderer>().enabled=false;
        for(int slot=0;slot<3;slot++) {
            room.SetStateImmediate(0,slot);
            if(room.Front.Passed&&room.Side.Passed) throw new Exception("Pillar alone bypasses combined projection");
        }
        checks.Add("borrow pillar alone rejected in all three slots");
        room.Pillar.gameObject.SetActive(false);room.RefreshProjection();
        if(room.Front.Passed||room.Side.Passed) throw new Exception("Borrow empty field bypass");
        var fill=GameObject.CreatePrimitive(PrimitiveType.Cube);fill.layer=9;
        fill.transform.position=room.Artifact.position;fill.transform.localScale=Vector3.one*4;
        room.RefreshProjection();
        if(room.Front.Passed||room.Side.Passed) throw new Exception("Borrow solid field bypass");
        UnityEngine.Object.DestroyImmediate(fill);
        room.Artifact.GetComponent<Renderer>().enabled=true;room.Pillar.gameObject.SetActive(true);
        room.SetStateImmediate(0,2);
        float[] sideCoverage=new float[3];
        for(int slot=0;slot<3;slot++) {room.SetStateImmediate(0,slot);sideCoverage[slot]=room.Side.DarkCoverage;}
        if(Mathf.Abs(sideCoverage[0]-sideCoverage[2])>.001f) throw new Exception("Lateral pillar motion must preserve the side projection");
        checks.Add("borrow empty/solid rejected; lateral motion preserves side projection");
        room.SetStateImmediate(2,0);
    }
    static void ValidateAlcove(ChamberController room,List<string> checks) {
        var secret=room.Alcove;secret.Initialize();int solutions=0;
        if(room.Solved) throw new Exception("Alcove test must run before courtyard completion");
        for(int pose=0;pose<4;pose++) for(int slot=0;slot<3;slot++) {
            room.SetStateImmediate(pose,0);room.SetArtifactSlotImmediate(slot);
            bool pass=secret.Front.Passed&&secret.Side.Passed;if(pass) solutions++;
            checks.Add("alcove pose="+pose+",socket="+slot+",pass="+pass);
            if(pose==1&&slot==2&&!pass) throw new Exception("Alcove goal failed");
        }
        if(solutions!=1) throw new Exception("Expected one alcove layout, found "+solutions);
        secret.Frame.gameObject.SetActive(false);
        for(int pose=0;pose<4;pose++) for(int slot=0;slot<3;slot++) {
            room.SetStateImmediate(pose,0);room.SetArtifactSlotImmediate(slot);
            if(secret.Front.Passed&&secret.Side.Passed) throw new Exception("Alcove can be bypassed without frame");
        }
        secret.Frame.gameObject.SetActive(true);room.Artifact.GetComponent<Renderer>().enabled=false;secret.Refresh();
        if(secret.Front.Passed&&secret.Side.Passed) throw new Exception("Frame alone solved alcove");
        secret.Frame.gameObject.SetActive(false);secret.Refresh();
        if(secret.Front.Passed||secret.Side.Passed) throw new Exception("Empty alcove bypass");
        var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.layer=12;cube.transform.position=new Vector3(.45f,1.65f,0);cube.transform.localScale=Vector3.one*5;secret.Refresh();
        if(secret.Front.Passed||secret.Side.Passed) throw new Exception("Solid alcove bypass");
        UnityEngine.Object.DestroyImmediate(cube);secret.Frame.gameObject.SetActive(true);room.Artifact.GetComponent<Renderer>().enabled=true;
        room.SetStateImmediate(2,0);room.SetArtifactSlotImmediate(1);
        checks.Add("alcove: no progress gate; frame and guardian both required; empty and solid rejected");
    }
    static void ValidateRelay(JourneyController journey,List<string> checks) {
        var relay=journey.Relay;var room=journey.Rooms[0];relay.Initialize();int solutions=0;
        for(int setting=0;setting<3;setting++) for(int pose=0;pose<4;pose++) for(int socket=0;socket<3;socket++) {
            relay.SetImmediate(setting);room.SetStateImmediate(pose,0);room.SetArtifactSlotImmediate(socket);
            bool pass=relay.Front.Passed&&relay.Side.Passed;if(pass) solutions++;
            checks.Add("relay gear="+setting+",pose="+pose+",socket="+socket+",pass="+pass);
            if(setting==1&&pose==0&&socket==2&&!pass) throw new Exception("Relay target failed");
            if(setting!=1&&pass) throw new Exception("Relay solved without physically setting the remote frame");
        }
        if(solutions!=1) throw new Exception("Unexpected relay solutions: "+solutions);
        relay.SetImmediate(1);room.SetStateImmediate(0,0);room.SetArtifactSlotImmediate(2);
        room.Alcove.Frame.gameObject.SetActive(false);relay.Refresh();
        if(relay.Front.Passed&&relay.Side.Passed) throw new Exception("Relay guardian-only bypass");
        room.Alcove.Frame.gameObject.SetActive(true);room.Artifact.GetComponent<Renderer>().enabled=false;relay.Refresh();
        if(relay.Front.Passed&&relay.Side.Passed) throw new Exception("Relay frame-only bypass");
        room.Alcove.Frame.gameObject.SetActive(false);relay.Refresh();
        if(relay.Front.Passed||relay.Side.Passed) throw new Exception("Relay empty field bypass");
        var solid=GameObject.CreatePrimitive(PrimitiveType.Cube);solid.layer=12;solid.transform.position=new Vector3(.45f,1.65f,0);solid.transform.localScale=Vector3.one*5;relay.Refresh();
        if(relay.Front.Passed||relay.Side.Passed) throw new Exception("Relay solid field bypass");
        UnityEngine.Object.DestroyImmediate(solid);room.Alcove.Frame.gameObject.SetActive(true);room.Artifact.GetComponent<Renderer>().enabled=true;
        relay.SetImmediate(0);room.SetStateImmediate(1,0);room.SetArtifactSlotImmediate(2);
        if(!room.Alcove.Front.Passed||!room.Alcove.Side.Passed) throw new Exception("Relay zero did not restore optional knowledge lock");
        room.SetStateImmediate(2,0);room.SetArtifactSlotImmediate(1);
        checks.Add("relay: 36 layouts; both meshes required; empty/solid rejected; zero restores original alcove");
    }
    static void ValidateBridge(JourneyController journey,List<string> checks) {
        var relay=journey.Relay;var bridge=journey.Bridge;var room=journey.Rooms[0];
        var rows=new List<string>{"route,gear,guardian_pose,guardian_socket,bridge_pose,bridge_depth,passed,dark_front,light_front,dark_side,light_side,space_clear"};
        relay.SetImmediate(3);bridge.Apply(false,0,1);
        for(int p=0;p<4;p++) for(int s=0;s<3;s++) {
            room.SetStateImmediate(p,0);room.SetArtifactSlotImmediate(s);
            if(relay.Front.Passed&&relay.Side.Passed) throw new Exception("Retracted frame solved without bridge");
        }
        int solutions=0;bool intended=false,startLegalButWrong=false;
        for(int p=0;p<4;p++) for(int s=0;s<3;s++) for(int bp=0;bp<4;bp++) for(int bs=0;bs<3;bs++) {
            room.SetStateImmediate(p,0);room.SetArtifactSlotImmediate(s);bridge.Apply(true,bp,bs);
            bool clear=bridge.CanPlace(bp,bs,p,s);bool pass=relay.Front.Passed&&relay.Side.Passed&&clear;if(pass) solutions++;
            if(p==0&&s==2&&bp==0&&bs==BridgePiece.SolvedDepth) intended=pass;
            if(p==0&&s==2&&bp==0&&bs==BridgePiece.StartDepth) startLegalButWrong=clear&&!pass;
            rows.Add(string.Join(",",new[]{"bridge","3",p.ToString(),s.ToString(),bp.ToString(),bs.ToString(),pass.ToString(),relay.Front.DarkCoverage.ToString("F4",System.Globalization.CultureInfo.InvariantCulture),relay.Front.LightOcclusion.ToString("F4",System.Globalization.CultureInfo.InvariantCulture),relay.Side.DarkCoverage.ToString("F4",System.Globalization.CultureInfo.InvariantCulture),relay.Side.LightOcclusion.ToString("F4",System.Globalization.CultureInfo.InvariantCulture),clear.ToString()}));
        }
        Directory.CreateDirectory("Validation");File.WriteAllLines("Validation/bridge-layouts.csv",rows);
        if(!intended) throw new Exception("Proposed bridge route failed; inspect bridge-layouts.csv before changing geometry or constraints");
        if(!startLegalButWrong) throw new Exception("Bridge start depth must be spatially legal yet unsolved; otherwise the depth lesson disappears");
        checks.Add("bridge alternative route verified; valid combinations="+solutions+" of 144 installed layouts; start depth "+BridgePiece.StartDepth+" legal but unsolved, solved depth "+BridgePiece.SolvedDepth);
        // The bridge alone must still need the generated guardian.
        room.Artifact.GetComponent<Renderer>().enabled=false;
        for(int p=0;p<4;p++) for(int s=0;s<3;s++) {
            bridge.Apply(true,p,s);if(relay.Front.Passed&&relay.Side.Passed) throw new Exception("Bridge alone bypass");
        }
        room.Artifact.GetComponent<Renderer>().enabled=true;bridge.Apply(false,0,1);relay.SetImmediate(0);room.SetStateImmediate(2,0);room.SetArtifactSlotImmediate(1);
        checks.Add("bridge alone rejected; retracted-frame route requires bridge; original scene restored");
    }
    // Depth lock: unsolvable while locked, no shortcuts in older puzzles once open.
    static void ValidateOffsetSeal(JourneyController journey,List<string> checks) {
        var room=journey.Rooms[0];var seal=room.Offset;var relay=journey.Relay;var alcove=room.Alcove;
        seal.Initialize();alcove.Initialize();relay.Initialize();
        relay.SetImmediate(1); // the final seal target is only reachable at gear 1; keep it there for the comparison
        int lockedSeal=0,openSeal=0,decoyFront=0,decoySide=0;bool intended=false;
        int doorLocked=0,doorOpen=0,alcoveLocked=0,alcoveOpen=0,relayLocked=0,relayOpen=0;
        for(int pose=0;pose<4;pose++) for(int slot=0;slot<3;slot++) for(int depth=0;depth<3;depth++) {
            room.SetStateImmediate(pose,0);room.SetArtifactSlotImmediate(slot);room.SetDepthImmediate(depth);
            bool locked=depth==ChamberController.DefaultDepth;
            bool sealPass=seal.Front.Passed&&seal.Side.Passed;
            if(sealPass) {openSeal++;if(locked) lockedSeal++;if(pose==OffsetSeal.TargetPose&&slot==OffsetSeal.TargetSlot&&depth==OffsetSeal.TargetDepth) intended=true;}
            else if(seal.Front.Passed) decoyFront++; else if(seal.Side.Passed) decoySide++;
            if(room.Front.Passed&&room.Side.Passed) {doorOpen++;if(locked) doorLocked++;}
            if(alcove.Front.Passed&&alcove.Side.Passed) {alcoveOpen++;if(locked) alcoveLocked++;}
            if(relay.Front.Passed&&relay.Side.Passed) {relayOpen++;if(locked) relayLocked++;}
        }
        if(lockedSeal!=0) throw new Exception("Offset seal is solvable without the depth clamp: "+lockedSeal+" layouts");
        if(!intended||openSeal!=1) throw new Exception("Offset seal needs exactly the intended solution, found "+openSeal+" (intended reachable: "+intended+")");
        if(doorOpen!=doorLocked) throw new Exception("Depth created "+(doorOpen-doorLocked)+" new shortcut(s) through the courtyard door");
        if(alcoveOpen!=alcoveLocked) throw new Exception("Depth created "+(alcoveOpen-alcoveLocked)+" new alcove route(s)");
        if(relayOpen!=relayLocked) throw new Exception("Depth created "+(relayOpen-relayLocked)+" new final-seal route(s)");
        relay.SetImmediate(0);room.SetDepthImmediate(ChamberController.DefaultDepth);room.SetStateImmediate(2,0);room.SetArtifactSlotImmediate(1);
        checks.Add("offset seal: 0 solutions with depth locked, exactly 1 of 108 layouts with depth open (one-window decoys: front "+decoyFront+", side "+decoySide+")");
        checks.Add("depth open creates no new routes: door "+doorLocked+"->"+doorOpen+", alcove "+alcoveLocked+"->"+alcoveOpen+", final seal at gear 1 "+relayLocked+"->"+relayOpen+" (borrow room does not read this guardian)");
    }
    // Gate seal: one arrangement only, neither object alone.
    static void ValidateGateSeal(ChamberController room,List<string> checks) {
        var seal=room.Gate;seal.Initialize();int solutions=0;bool intended=false;int frontOnly=0,sideOnly=0;
        for(int pose=0;pose<4;pose++) for(int seat=0;seat<3;seat++) for(int depth=0;depth<3;depth++) {
            room.SetStateImmediate(pose,0);room.SetArtifactSlotImmediate(seat);room.SetDepthImmediate(depth);
            bool f=seal.Front.Passed,s=seal.Side.Passed;
            if(f&&s) {solutions++;if(pose==GateSeal.TargetPose&&seat==GateSeal.TargetSlot&&depth==GateSeal.TargetDepth) intended=true;}
            else if(f) frontOnly++; else if(s) sideOnly++;
        }
        if(!intended) throw new Exception("Gate seal target not reachable at its own arrangement");
        // The design claim: standing in the middle blocks the gate's opening. Prove it, do not assume it.
        room.SetStateImmediate(GateSeal.TargetPose,0);room.SetArtifactSlotImmediate(0);room.SetDepthImmediate(GateSeal.TargetDepth);
        if(seal.Front.Passed) throw new Exception("Filling the opening completely must fail; the viewfinder target is not the full opening");
        if(solutions!=1) throw new Exception("Gate seal must have exactly one solution, found "+solutions);
        // Neither object alone may pass.
        seal.Gate.gameObject.SetActive(false);seal.Refresh();
        if(seal.Front.Passed&&seal.Side.Passed) throw new Exception("Gate seal solved without the gate stone");
        seal.Gate.gameObject.SetActive(true);room.Artifact.GetComponent<Renderer>().enabled=false;seal.Refresh();
        if(seal.Front.Passed&&seal.Side.Passed) throw new Exception("Gate seal solved without the guardian");
        room.Artifact.GetComponent<Renderer>().enabled=true;seal.Refresh();
        // The layer split must keep the stone out of the older windows.
        room.SetDepthImmediate(ChamberController.DefaultDepth);room.SetStateImmediate(2,0);room.SetArtifactSlotImmediate(1);seal.Refresh();
        checks.Add("gate seal: 36 layouts, exactly 1 solution (pose "+GateSeal.TargetPose+", seat "+GateSeal.TargetSlot+", depth "+GateSeal.TargetDepth+"); decoys front "+frontOnly+", side "+sideOnly+"; either object alone rejected");
    }
    // Hidden seal: one arrangement, no progress gate.
    static void ValidatePillarAlcove(ChamberController room,List<string> checks) {
        var alcove=room.PillarAlcove;alcove.Initialize();int solutions=0;bool intended=false;
        for(int pose=0;pose<4;pose++) for(int slot=0;slot<3;slot++) {
            room.SetStateImmediate(pose,slot);
            bool pass=alcove.Front.Passed&&alcove.Side.Passed;if(pass) solutions++;
            if(pose==PillarAlcove.TargetPose&&slot==PillarAlcove.TargetPillar) intended=pass;
        }
        if(!intended) throw new Exception("Pillar alcove target not reachable");
        if(solutions!=1) throw new Exception("Pillar alcove must have exactly one solution, found "+solutions);
        room.Pillar.gameObject.SetActive(false);alcove.Refresh();
        if(alcove.Front.Passed&&alcove.Side.Passed) throw new Exception("Pillar alcove solved without the pillar");
        room.Pillar.gameObject.SetActive(true);room.Artifact.GetComponent<Renderer>().enabled=false;alcove.Refresh();
        if(alcove.Front.Passed&&alcove.Side.Passed) throw new Exception("Pillar alcove solved without the guardian");
        room.Artifact.GetComponent<Renderer>().enabled=true;room.SetStateImmediate(0,2);alcove.Refresh();
        checks.Add("pillar alcove: 12 layouts, exactly 1 solution (pose "+PillarAlcove.TargetPose+", pillar "+PillarAlcove.TargetPillar+"); either object alone rejected");
    }
    [MenuItem("Twin View/Validate and build Windows demo")]
    public static void BuildAll() {
        BuildScene();
        var rooms=UnityEngine.Object.FindObjectOfType<JourneyController>().Rooms;
        var controller=rooms[0];controller.Initialize();
        var checks=new List<string>();
        for(int pose=0;pose<4;pose++) for(int socket=0;socket<3;socket++) {
            controller.SetPoseImmediate(pose);
            controller.SetArtifactSlotImmediate(socket);
            bool f=controller.Front.Passed,s=controller.Side.Passed;
            checks.Add("courtyard pose="+pose+",socket="+socket+",front="+f+",side="+s);
            if(pose==0&&socket==1&&!(f&&s))throw new Exception("Goal pose must solve both windows");
            if(pose==2&&socket==1&&(!f||s))throw new Exception("Reverse pose must solve only the front window");
            if(!(pose==0&&socket==1)&&f&&s)throw new Exception("Moving to alcove sockets bypassed the courtyard puzzle");
        }
        controller.SetArtifactSlotImmediate(1);
        var meshRenderer=controller.Artifact.GetComponent<Renderer>();meshRenderer.enabled=false;controller.RefreshProjection();
        if(controller.Front.Passed||controller.Side.Passed)throw new Exception("Empty field bypass");checks.Add("empty rejected");
        meshRenderer.enabled=true;
        var cheat=GameObject.CreatePrimitive(PrimitiveType.Cube);cheat.layer=8;cheat.transform.position=controller.Artifact.position;cheat.transform.localScale=Vector3.one*3;
        controller.RefreshProjection();if(controller.Front.Passed||controller.Side.Passed)throw new Exception("Solid fill bypass");
        UnityEngine.Object.DestroyImmediate(cheat);checks.Add("solid fill rejected");
        controller.SetPoseImmediate(2);
        ValidateBorrowRoom(rooms[1],checks);
        ValidateAlcove(controller,checks);
        ValidateRelay(UnityEngine.Object.FindObjectOfType<JourneyController>(),checks);
        ValidateBridge(UnityEngine.Object.FindObjectOfType<JourneyController>(),checks);
        ValidateOffsetSeal(UnityEngine.Object.FindObjectOfType<JourneyController>(),checks);
        ValidateGateSeal(controller,checks);
        ValidatePillarAlcove(rooms[1],checks);
        SolverReport.WriteFinalSealReport(UnityEngine.Object.FindObjectOfType<JourneyController>(),checks);
        Directory.CreateDirectory("Validation");File.WriteAllLines("Validation/editor-checks.txt",checks);
        // Reload the clean scene to drop calibration objects.
        EditorSceneManager.OpenScene("Assets/Scenes/FirstChamber.unity");
        Directory.CreateDirectory("Builds/Windows");
        var report=BuildPipeline.BuildPlayer(new[]{"Assets/Scenes/FirstChamber.unity"},"Builds/Windows/TwinViewBox.exe",BuildTarget.StandaloneWindows64,BuildOptions.None);
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed: "+report.summary.result);
        Debug.Log("TWINVIEW_VALIDATION_AND_BUILD_PASSED");
    }
}
