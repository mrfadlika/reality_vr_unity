import './style.css';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { FBXLoader } from 'three/examples/jsm/loaders/FBXLoader.js';
import { StereoCamera } from 'three';

// Configuration
const API_URL = 'https://apps.globalhalalhub.co.id/global-halal-api/api/vr/getCtrLoc';
const slotSpacing = 6.1; 
const rowSpacing = 2.8;  
const tierSpacing = 2.6; 
const YARD_OFFSETS = {
    'A': { x: 0, z: 0 },
    'B': { x: 0, z: 30 },
    'C': { x: 0, z: 60 },
    'D': { x: 0, z: 90 }
};

// DOM Elements
const vrBtn = document.getElementById('vr-toggle');
const loading = document.getElementById('loading');
const app = document.getElementById('app');

let canvas = document.getElementById('canvas');
if (!canvas) {
    canvas = document.createElement('canvas');
    canvas.id = 'canvas';
    app.appendChild(canvas);
}

// State
let isVRMode = false;
let isMobile = /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent);

// Three.js Core
const scene = new THREE.Scene();
scene.background = new THREE.Color(0x0f172a);
scene.fog = new THREE.FogExp2(0x0f172a, 0.005);

// Camera Setup
const camera = new THREE.PerspectiveCamera(60, window.innerWidth / window.innerHeight, 0.1, 1000);
camera.position.set(50, 40, 50);

const vrCameraRig = new THREE.Group();
vrCameraRig.position.set(20, 1.6, 15);
scene.add(vrCameraRig);
vrCameraRig.add(camera);

let renderer;
try {
    // Disable antialiasing and set precision to mediump to bypass GPU driver bugs
    renderer = new THREE.WebGLRenderer({ 
        canvas, 
        antialias: false,
        powerPreference: "high-performance",
        precision: "mediump"
    });
    renderer.setPixelRatio(1); // Force pixel ratio to 1 for better compatibility
    renderer.setSize(window.innerWidth, window.innerHeight);
    renderer.shadowMap.enabled = false; // Disable shadows temporarily
} catch (e) {
    loading.innerHTML = "WebGL Error: " + e.message + "<br><br>Please Hard Refresh (Ctrl+F5) or restart your browser.";
    loading.style.color = "#ef4444";
    throw e;
}

const ambientLight = new THREE.AmbientLight(0xffffff, 0.6);
scene.add(ambientLight);

const dirLight = new THREE.DirectionalLight(0xffffff, 0.8);
dirLight.position.set(100, 200, 50);
dirLight.castShadow = true;
scene.add(dirLight);

// Environment: Ground, Yards, Roads
const groundGeo = new THREE.PlaneGeometry(300, 300);
const groundMat = new THREE.MeshBasicMaterial({ color: 0x111827 }); // Asphalt color
const ground = new THREE.Mesh(groundGeo, groundMat);
ground.rotation.x = -Math.PI / 2;
scene.add(ground);

// Draw Yard Concrete Pads
const yardKeys = Object.keys(YARD_OFFSETS);
yardKeys.forEach(key => {
    const offset = YARD_OFFSETS[key];
    const padGeo = new THREE.PlaneGeometry(80, 24); 
    const padMat = new THREE.MeshBasicMaterial({ color: 0x1f2937 }); // Concrete
    const pad = new THREE.Mesh(padGeo, padMat);
    pad.rotation.x = -Math.PI / 2;
    pad.position.set(offset.x + 35, 0.01, offset.z + 10);
    scene.add(pad);

    // Add Text Sprite/Label for Yard if possible (using simple blocks for now)
});

// Draw Roads (dashed lines between yards)
const roadGeo = new THREE.PlaneGeometry(100, 1);
const roadMat = new THREE.MeshBasicMaterial({ color: 0xf59e0b }); // Yellow line
for (let i = 1; i < yardKeys.length; i++) {
    const zPos = YARD_OFFSETS[yardKeys[i]].z - 3; // Center of the gap
    const roadLine = new THREE.Mesh(roadGeo, roadMat);
    roadLine.rotation.x = -Math.PI / 2;
    roadLine.position.set(35, 0.02, zPos);
    scene.add(roadLine);
}

let orbitControls = new OrbitControls(camera, renderer.domElement);
orbitControls.target.set(30, 0, 30);
orbitControls.maxPolarAngle = Math.PI / 2 - 0.05;
orbitControls.update();

let deviceControls = null;
let stereoCamera = new StereoCamera();

// Custom Device Orientation handler for VR Head tracking
let deviceOrientation = { alpha: 0, beta: 0, gamma: 0 };
let screenOrientation = window.orientation || 0;

window.addEventListener('orientationchange', () => {
    screenOrientation = window.orientation || 0;
});

function connectGyro() {
    window.addEventListener('deviceorientation', (event) => {
        deviceOrientation.alpha = event.alpha ? THREE.MathUtils.degToRad(event.alpha) : 0;
        deviceOrientation.beta = event.beta ? THREE.MathUtils.degToRad(event.beta) : 0;
        deviceOrientation.gamma = event.gamma ? THREE.MathUtils.degToRad(event.gamma) : 0;
    }, false);
}

// Handle VR Toggle
if (isMobile) {
    vrBtn.classList.remove('hidden');
    vrBtn.addEventListener('click', () => {
        isVRMode = !isVRMode;
        if (isVRMode) {
            app.classList.add('vr-active');
            vrBtn.innerText = "Exit VR";
            orbitControls.enabled = false;
            
            camera.position.set(0, 0, 0);
            camera.rotation.set(0, 0, 0);

            // Request permission for iOS 13+ devices
            if (typeof DeviceOrientationEvent !== 'undefined' && typeof DeviceOrientationEvent.requestPermission === 'function') {
                DeviceOrientationEvent.requestPermission()
                    .then(response => {
                        if (response == 'granted') connectGyro();
                    }).catch(console.error);
            } else {
                connectGyro();
            }
        } else {
            app.classList.remove('vr-active');
            vrBtn.innerText = "Enter VR Mode";
            orbitControls.enabled = true;
            camera.position.set(50, 40, 50);
            camera.lookAt(30, 0, 30);
        }
    });
}

// FBX Models Cache
const models = {
    '20': null,
    '40': null
};

const fbxLoader = new FBXLoader();

async function loadModels() {
    return new Promise((resolve) => {
        let loaded = 0;
        const checkDone = () => {
            loaded++;
            if (loaded === 2) resolve();
        };

        fbxLoader.load('/models/20feet.fbx', (object) => {
            object.scale.set(0.01, 0.01, 0.01); 
            object.traverse((child) => {
                if (child.isMesh) {
                    // Fallback to BasicMaterial to completely bypass lighting uniform limit on older GPUs
                    const oldColor = child.material.color || new THREE.Color(0x3b82f6);
                    const oldMap = child.material.map || null;
                    child.material = new THREE.MeshBasicMaterial({ color: oldColor, map: oldMap });
                }
            });
            models['20'] = object;
            checkDone();
        }, undefined, (e) => { console.error(e); checkDone(); });

        fbxLoader.load('/models/40feet.fbx', (object) => {
            object.scale.set(0.01, 0.01, 0.01); 
            object.traverse((child) => {
                if (child.isMesh) {
                    const oldColor = child.material.color || new THREE.Color(0xf59e0b);
                    const oldMap = child.material.map || null;
                    child.material = new THREE.MeshBasicMaterial({ color: oldColor, map: oldMap });
                }
            });
            models['40'] = object;
            checkDone();
        }, undefined, (e) => { console.error(e); checkDone(); });
    });
}

async function fetchContainerData() {
    try {
        const response = await fetch(API_URL);
        const data = await response.json();
        return data;
    } catch (e) {
        console.error("Failed to fetch API", e);
        return [];
    }
}

function spawnContainers(data) {
    const containersGroup = new THREE.Group();
    scene.add(containersGroup);

    const blockColors = {
        'A': 0xef4444, // Red
        'B': 0x3b82f6, // Blue
        'C': 0x10b981, // Green
        'D': 0xf59e0b  // Orange
    };

    data.forEach(ctr => {
        const yardBlock = ctr.YARD_BLOCK.toUpperCase();
        const slot = parseInt(ctr.YARD_SLOT) || 1;
        const row = parseInt(ctr.YARD_ROW) || 1;
        const tier = parseInt(ctr.YARD_TIER) || 1;
        const size = ctr.CTR_SIZE === "40" ? "40" : "20";

        const template = models[size];
        if (!template) return;

        const clone = template.clone();
        
        // Fix rotation (FBX sometimes imports standing on end)
        clone.rotation.x = -Math.PI / 2;
        
        // Apply color and outline
        const colorHex = blockColors[yardBlock] || 0x8b5cf6; // Default purple
        
        clone.traverse((child) => {
            if (child.isMesh) {
                // Unique material per container
                child.material = child.material.clone();
                child.material.color.setHex(colorHex);
                
                // Add black outline to give 3D depth without lighting
                const edges = new THREE.EdgesGeometry(child.geometry);
                const line = new THREE.LineSegments(edges, new THREE.LineBasicMaterial({ color: 0x000000, linewidth: 2, opacity: 0.5, transparent: true }));
                child.add(line);
            }
        });

        // Calculate Position
        const yardOffset = YARD_OFFSETS[yardBlock] || {x: 0, z: 0};
        
        const xPos = (slot - 1) * slotSpacing + yardOffset.x;
        const yPos = (tier - 1) * tierSpacing;
        const zPos = (row - 1) * rowSpacing + yardOffset.z;

        clone.position.set(xPos, yPos, zPos);
        containersGroup.add(clone);
    });

    return containersGroup;
}

// Window Resize
window.addEventListener('resize', onWindowResize, false);
function onWindowResize() {
    camera.aspect = window.innerWidth / window.innerHeight;
    camera.updateProjectionMatrix();
    renderer.setSize(window.innerWidth, window.innerHeight);
}

const euler = new THREE.Euler(0, 0, 0, 'YXZ');
const zee = new THREE.Vector3(0, 0, 1);
const q0 = new THREE.Quaternion();
const q1 = new THREE.Quaternion(-Math.sqrt(0.5), 0, 0, Math.sqrt(0.5));

// Render Loop
function animate() {
    requestAnimationFrame(animate);

    if (isVRMode) {
        // Apply Gyroscope orientation
        euler.set(deviceOrientation.beta, deviceOrientation.alpha, -deviceOrientation.gamma, 'YXZ');
        camera.quaternion.setFromEuler(euler);
        camera.quaternion.multiply(q1);
        camera.quaternion.multiply(q0.setFromAxisAngle(zee, -screenOrientation));
        
        renderer.setScissorTest(true);

        // Left Eye
        renderer.setScissor(0, 0, window.innerWidth / 2, window.innerHeight);
        renderer.setViewport(0, 0, window.innerWidth / 2, window.innerHeight);
        stereoCamera.update(camera);
        renderer.render(scene, stereoCamera.cameraL);

        // Right Eye
        renderer.setScissor(window.innerWidth / 2, 0, window.innerWidth / 2, window.innerHeight);
        renderer.setViewport(window.innerWidth / 2, 0, window.innerWidth / 2, window.innerHeight);
        renderer.render(scene, stereoCamera.cameraR);
        
        renderer.setScissorTest(false);
    } else {
        orbitControls.update();
        renderer.setViewport(0, 0, window.innerWidth, window.innerHeight);
        renderer.render(scene, camera);
    }
}

// Init
async function init() {
    await loadModels();
    
    // Fallback simple colored boxes if FBX failed to load
    if (!models['20']) {
        const geo20 = new THREE.BoxGeometry(6.1, 2.6, 2.4);
        const mat20 = new THREE.MeshStandardMaterial({ color: 0x3b82f6 });
        models['20'] = new THREE.Mesh(geo20, mat20);
    }
    if (!models['40']) {
        const geo40 = new THREE.BoxGeometry(12.2, 2.6, 2.4);
        const mat40 = new THREE.MeshStandardMaterial({ color: 0xf59e0b });
        models['40'] = new THREE.Mesh(geo40, mat40);
    }

    const data = await fetchContainerData();
    spawnContainers(data);
    
    loading.classList.add('hidden');
    animate();
}

init();

// Handle Vite Hot Module Replacement (HMR) to prevent WebGL context leaks
if (import.meta.hot) {
    import.meta.hot.dispose(() => {
        if (renderer) {
            renderer.dispose();
            renderer.forceContextLoss();
            const canvas = renderer.domElement;
            if (canvas && canvas.parentNode) {
                canvas.parentNode.removeChild(canvas);
            }
        }
    });
}
