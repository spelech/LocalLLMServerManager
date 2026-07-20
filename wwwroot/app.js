// Local LLM Server Manager UI Logic

// State management
let localModels = [];
let loadedModels = [];
let hfSearchResults = [];
let currentPullController = null;
let gpuTotalVram = 8 * 1024 * 1024 * 1024; // Default to 8 GB until detected
const modelMetadataCache = new Map();

// Strengths database mapping
const modelStrengths = [
    {
        matches: (/** @type {string} */ name) => name.includes('coder') || name.includes('code'),
        tags: ['💻 Coding', '🛠️ Debugging', '📝 Code Generation'],
        color: 'hsl(190, 85%, 35%)',
        desc: 'Optimized for writing, explaining, and debugging code in Python, JS, C#, and other languages.'
    },
    {
        matches: (/** @type {string} */ name) => name.includes('math') || name.includes('calc'),
        tags: ['🧮 Mathematics', '🧠 Logic', '📊 Reasoning'],
        color: 'hsl(280, 75%, 45%)',
        desc: 'Enhanced capabilities for arithmetic word problems, algebraic calculations, and logic puzzles.'
    },
    {
        matches: (/** @type {string} */ name) => name.includes('r1') || name.includes('reasoning') || name.includes('deepseek-r1'),
        tags: ['🧠 Reasoning', '🔬 Chain of Thought', '💡 Complex Logic'],
        color: 'hsl(258, 80%, 45%)',
        desc: 'Deep thinking model which outputs detailed step-by-step logic before returning the final response.'
    },
    {
        matches: (/** @type {string} */ name, /** @type {string} */ family) => family === 'gemma' || name.includes('gemma'),
        tags: ['💎 Chat', '📋 Instruction Following', '🗣️ Multi-turn'],
        color: 'hsl(14, 80%, 40%)',
        desc: 'Google\'s Gemma family. Highly capable in general reasoning, text creation, and structured API queries.'
    },
    {
        matches: (/** @type {string} */ name, /** @type {string} */ family) => family === 'llama' || name.includes('llama'),
        tags: ['🦙 Generalist', '📖 Summary', '⚡ High Efficiency'],
        color: 'hsl(150, 70%, 30%)',
        desc: 'Meta\'s Llama family. Outstanding general-purpose model for chat, documents summaries, and tool usage.'
    },
    {
        matches: (/** @type {string} */ name, /** @type {string} */ family) => family === 'qwen' || name.includes('qwen'),
        tags: ['🏮 Multilingual', '📑 Writing', '🔍 Data Extraction'],
        color: 'hsl(340, 75%, 40%)',
        desc: 'Alibaba\'s Qwen family. Exceptional performance in non-English translation, writing, and agentic tools.'
    },
    {
        matches: (/** @type {string} */ name, /** @type {string} */ family) => family === 'phi' || name.includes('phi'),
        tags: ['📐 Logical Reasoning', '📋 Summarization', '⚡ Low Latency'],
        color: 'hsl(210, 80%, 40%)',
        desc: 'Microsoft\'s Phi family. Extremely compact yet highly intelligent models trained on logic and textbook data.'
    }
];

// Determine capabilities profile
function getModelCapabilities(/** @type {string} */ name, /** @type {string} */ family) {
    const n = name.toLowerCase();
    const f = (family || '').toLowerCase();
    
    for (const profile of modelStrengths) {
        if (profile.matches(n, f)) {
            return profile;
        }
    }
    
    return {
        tags: ['💬 Conversation', '📝 General Text'],
        color: 'hsl(210, 10%, 40%)',
        desc: 'General purpose model suitable for daily dialogue, editing, summaries, and text completion.'
    };
}

// Estimate KV Cache Bytes Per Token for external models
function estimateKvCachePerToken(/** @type {string} */ repoId) {
    const id = repoId.toLowerCase();
    if (id.includes('gemma-2-2b') || id.includes('2b')) {
        return 2 * 26 * 8 * 256 * 2; // ~106 KB
    }
    if (id.includes('gemma-2-9b') || id.includes('9b') || id.includes('8b')) {
        return 2 * 32 * 8 * 128 * 2; // ~65 KB
    }
    if (id.includes('70b') || id.includes('72b')) {
        return 2 * 80 * 8 * 128 * 2; // ~163 KB
    }
    return 65536; // Default safe estimate (roughly 64KB per token)
}

// DOM Elements
const ollamaHealthEl = /** @type {HTMLElement} */ (document.getElementById('ollama-health'));
const forgeHealthEl = /** @type {HTMLElement} */ (document.getElementById('forge-health'));
const statTotalModelsEl = /** @type {HTMLElement} */ (document.getElementById('stat-total-models'));
const statVramModelsEl = /** @type {HTMLElement} */ (document.getElementById('stat-vram-models'));
const statGpuNameEl = /** @type {HTMLElement} */ (document.getElementById('stat-gpu-name'));
const btnUnloadAll = /** @type {HTMLButtonElement} */ (document.getElementById('btn-unload-all'));
const installedModelsGrid = /** @type {HTMLElement} */ (document.getElementById('installed-models-grid'));

// Visual VRAM elements
const vramBarLoaded = /** @type {HTMLElement} */ (document.getElementById('vram-bar-loaded'));
const vramBarFree = /** @type {HTMLElement} */ (document.getElementById('vram-bar-free'));
const vramLabelUsed = /** @type {HTMLElement} */ (document.getElementById('vram-label-used'));
const vramLabelTotal = /** @type {HTMLElement} */ (document.getElementById('vram-label-total'));

const tabLinks = /** @type {NodeListOf<HTMLElement>} */ (document.querySelectorAll('.tab-link'));
const tabContents = /** @type {NodeListOf<HTMLElement>} */ (document.querySelectorAll('.tab-content'));
const subtabLinks = /** @type {NodeListOf<HTMLElement>} */ (document.querySelectorAll('.subtab-link'));
const subtabContents = /** @type {NodeListOf<HTMLElement>} */ (document.querySelectorAll('.subtab-content'));

const hfSearchForm = /** @type {HTMLFormElement} */ (document.getElementById('hf-search-form'));
const hfSearchInput = /** @type {HTMLInputElement} */ (document.getElementById('hf-search-input'));
const hfResultsContainer = /** @type {HTMLElement} */ (document.getElementById('hf-results-container'));
const tagChips = /** @type {NodeListOf<HTMLElement>} */ (document.querySelectorAll('.tag-chip'));

const customPullForm = /** @type {HTMLFormElement} */ (document.getElementById('custom-pull-form'));
const customPullInput = /** @type {HTMLInputElement} */ (document.getElementById('custom-pull-input'));

const progressDrawer = /** @type {HTMLElement} */ (document.getElementById('progress-drawer'));
const pullModelNameEl = /** @type {HTMLElement} */ (document.getElementById('pull-model-name'));
const pullStatusBadge = /** @type {HTMLElement} */ (document.getElementById('pull-status-badge'));
const pullProgressPercent = /** @type {HTMLElement} */ (document.getElementById('pull-progress-percent'));
const pullProgressBytes = /** @type {HTMLElement} */ (document.getElementById('pull-progress-bytes'));
const pullProgressFill = /** @type {HTMLElement} */ (document.getElementById('pull-progress-fill'));
const pullStatusLog = /** @type {HTMLElement} */ (document.getElementById('pull-status-log'));

const hfModelModal = /** @type {HTMLElement} */ (document.getElementById('hf-model-modal'));
const modalRepoId = /** @type {HTMLElement} */ (document.getElementById('modal-repo-id'));
const modalAuthorName = /** @type {HTMLElement} */ (document.getElementById('modal-author-name'));
const modalLikesCount = /** @type {HTMLElement} */ (document.getElementById('modal-likes-count'));
const modalFilesLoading = /** @type {HTMLElement} */ (document.getElementById('modal-files-loading'));
const modalFilesList = /** @type {HTMLElement} */ (document.getElementById('modal-files-list'));
const modalCustomPullString = /** @type {HTMLInputElement} */ (document.getElementById('modal-custom-pull-string'));
const btnModalPullCustom = /** @type {HTMLButtonElement} */ (document.getElementById('btn-modal-pull-custom'));
const btnCloseModal = /** @type {HTMLButtonElement} */ (document.getElementById('btn-close-modal'));

// Modal strengths
const modalStrengthsBox = /** @type {HTMLElement} */ (document.getElementById('modal-strengths-box'));
const modalStrengthsTags = /** @type {HTMLElement} */ (document.getElementById('modal-strengths-tags'));
const modalStrengthsDesc = /** @type {HTMLElement} */ (document.getElementById('modal-strengths-desc'));

// Modal VRAM components
const modalEstimator = /** @type {HTMLElement} */ (document.getElementById('modal-estimator'));
const modalCtxSlider = /** @type {HTMLInputElement} */ (document.getElementById('modal-ctx-slider'));
const modalSliderVal = /** @type {HTMLElement} */ (document.getElementById('modal-slider-val'));
const modalWeightsVal = /** @type {HTMLElement} */ (document.getElementById('modal-weights-val'));
const modalCacheVal = /** @type {HTMLElement} */ (document.getElementById('modal-cache-val'));
const modalTotalEstVal = /** @type {HTMLElement} */ (document.getElementById('modal-total-est-val'));
const modalEstBarFill = /** @type {HTMLElement} */ (document.getElementById('modal-est-bar-fill'));
const modalEstStatusMsg = /** @type {HTMLElement} */ (document.getElementById('modal-est-status-msg'));

let modalSelectedFileSize = 0;
let modalSelectedRepoId = '';

// Toast Notification
function showToast(message, type = 'info') {
    const toast = document.getElementById('toast');
    if (!toast) return;
    toast.textContent = message;
    toast.className = `toast active ${type}`;
    setTimeout(() => {
        toast.classList.remove('active');
    }, 4000);
}

// Format bytes helper
function formatBytes(bytes) {
    if (bytes === 0 || isNaN(bytes)) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
}

// Extract quantization tag from filename
function extractQuantization(filename) {
    if (!filename) return null;
    const regex = /(q[0-9]_[k0-9_]+[a-zA-Z]?|fp16|f16|bf16)/i;
    const match = filename.match(regex);
    return match ? match[1].toUpperCase() : null;
}

// Initialize Application
document.addEventListener('DOMContentLoaded', async () => {
    // Detect total GPU VRAM
    await detectGpuVram();

    // Initial health check and local loading
    checkHealth();
    loadLocalModels();

    // Set intervals for background updates
    setInterval(checkHealth, 5000);
    setInterval(updateVramUsageOnly, 8000);

    // Tab switching
    tabLinks.forEach(link => {
        link.addEventListener('click', () => {
            const targetTab = link.dataset.tab;
            
            tabLinks.forEach(l => l.classList.remove('active'));
            tabContents.forEach(c => c.classList.remove('active'));
            
            link.classList.add('active');
            const targetEl = document.getElementById(targetTab);
            if (targetEl) targetEl.classList.add('active');
        });
    });

    // Sub-tab switching (HF vs Ollama)
    subtabLinks.forEach(link => {
        link.addEventListener('click', () => {
            const targetSubtab = link.dataset.subtab;
            
            subtabLinks.forEach(l => l.classList.remove('active'));
            subtabContents.forEach(c => c.classList.remove('active'));
            
            link.classList.add('active');
            const targetEl = document.getElementById(targetSubtab);
            if (targetEl) targetEl.classList.add('active');
        });
    });

    // HF Search Form Submission
    if (hfSearchForm) {
        hfSearchForm.addEventListener('submit', (e) => {
            e.preventDefault();
            const query = hfSearchInput.value.trim();
            if (query) {
                searchHuggingFace(query);
            }
        });
    }

    // Custom Pull Form Submission
    if (customPullForm) {
        customPullForm.addEventListener('submit', (e) => {
            e.preventDefault();
            const modelName = customPullInput.value.trim();
            if (modelName) {
                pullModel(modelName);
                customPullInput.value = '';
            }
        });
    }

    // Quick Tags Click
    tagChips.forEach(chip => {
        chip.addEventListener('click', () => {
            const query = chip.dataset.query;
            hfSearchInput.value = query;
            searchHuggingFace(query);
        });
    });

    // Preset pull buttons
    document.body.addEventListener('click', (e) => {
        const target = /** @type {HTMLElement} */ (e.target);
        if (target && target.classList.contains('pull-preset-btn')) {
            const model = target.dataset.model;
            if (model) {
                pullModel(model);
            }
        }
    });

    // Unload All VRAM Button
    if (btnUnloadAll) {
        btnUnloadAll.addEventListener('click', async () => {
            if (loadedModels.length === 0) return;
            
            btnUnloadAll.disabled = true;
            btnUnloadAll.textContent = 'Unloading...';
            
            try {
                showToast(`Unloading ${loadedModels.length} model(s) from VRAM...`, 'info');
                
                for (const model of loadedModels) {
                    await fetch('/api/generate', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ model: model.name, keep_alive: 0 })
                    });
                }
                
                await new Promise(resolve => setTimeout(resolve, 1500)); // wait for VRAM clear
                showToast('All models unloaded from VRAM successfully.', 'success');
            } catch {
                showToast('Failed to unload some models.', 'error');
            } finally {
                btnUnloadAll.disabled = false;
                btnUnloadAll.textContent = 'Unload VRAM';
                loadLocalModels();
            }
        });
    }

    // Modal Close
    if (btnCloseModal) {
        btnCloseModal.addEventListener('click', () => {
            hfModelModal.classList.remove('active');
        });
    }
    
    window.addEventListener('click', (e) => {
        if (e.target === hfModelModal) {
            hfModelModal.classList.remove('active');
        }
    });

    // Modal Context Length Slider listener
    if (modalCtxSlider) {
        modalCtxSlider.addEventListener('input', () => {
            updateModalVramEstimation();
        });
    }
});

// Detect GPU VRAM size natively via backend API
async function detectGpuVram() {
    try {
        const response = await fetch('/api/gpu/vram');
        if (response.ok) {
            const data = await response.json();
            if (data && data.totalVramBytes > 0) {
                gpuTotalVram = data.totalVramBytes;
                vramLabelTotal.textContent = `${(gpuTotalVram / (1024 * 1024 * 1024)).toFixed(1)} GB Total`;
                if (statGpuNameEl && data.gpuName) {
                    statGpuNameEl.textContent = data.gpuName;
                }
            }
        }
    } catch (err) {
        console.error('Failed to detect native GPU memory:', err);
    }
}

// Check Server Health
async function checkHealth() {
    try {
        const response = await fetch('/health');
        if (response.ok) {
            const data = await response.json();
            
            // Ollama UI update
            const ollamaIndicator = ollamaHealthEl.querySelector('.status-indicator');
            const ollamaText = ollamaHealthEl.querySelector('.status-text');
            if (data.ollama === 'Online') {
                ollamaIndicator.className = 'status-indicator online';
                ollamaText.textContent = 'Online';
                ollamaText.className = 'status-text text-online';
            } else {
                ollamaIndicator.className = 'status-indicator offline';
                ollamaText.textContent = 'Offline';
                ollamaText.className = 'status-text text-offline';
            }

            // Stable Diffusion UI update
            const forgeIndicator = forgeHealthEl.querySelector('.status-indicator');
            const forgeText = forgeHealthEl.querySelector('.status-text');
            if (data.stableDiffusion === 'Online') {
                forgeIndicator.className = 'status-indicator online';
                forgeText.textContent = 'Online';
                forgeText.className = 'status-text text-online';
            } else {
                forgeIndicator.className = 'status-indicator offline';
                forgeText.textContent = 'Offline';
                forgeText.className = 'status-text text-offline';
            }
        }
    } catch {
        // Fallback to offline representation
        ollamaHealthEl.querySelector('.status-indicator').className = 'status-indicator offline';
        ollamaHealthEl.querySelector('.status-text').textContent = 'Offline';
        ollamaHealthEl.querySelector('.status-text').className = 'status-text text-offline';
        
        forgeHealthEl.querySelector('.status-indicator').className = 'status-indicator offline';
        forgeHealthEl.querySelector('.status-text').textContent = 'Offline';
        forgeHealthEl.querySelector('.status-text').className = 'status-text text-offline';
    }
}

// Retrieve detailed metadata via /api/show for local models
async function fetchModelMetadata(modelName) {
    if (modelMetadataCache.has(modelName)) {
        return modelMetadataCache.get(modelName);
    }
    
    try {
        const response = await fetch('/api/show', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ model: modelName })
        });
        
        if (response.ok) {
            const data = await response.json();
            const modelInfo = data.model_info || {};
            
            // Auto detect architecture namespace prefix (e.g. llama, gemma2, etc.)
            let prefix = 'llama';
            for (const key in modelInfo) {
                if (key.endsWith('.block_count')) {
                    prefix = key.split('.')[0];
                    break;
                }
            }
            
            const layers = Number(modelInfo[`${prefix}.block_count`] || 32);
            const kvHeads = Number(modelInfo[`${prefix}.attention.head_count_kv`] || 8);
            const qHeads = Number(modelInfo[`${prefix}.attention.head_count`] || 32);
            const embedLen = Number(modelInfo[`${prefix}.embedding_length`] || 4096);
            const maxCtx = Number(modelInfo[`${prefix}.context_length`] || 8192);
            const headDim = embedLen / qHeads;
            
            // KV Cache bytes per token multiplier (Key & Value vectors in FP16 precision)
            const kvCacheBytesPerToken = 2 * layers * kvHeads * headDim * 2;
            
            const metadata = { layers, kvHeads, qHeads, embedLen, maxCtx, headDim, kvCacheBytesPerToken };
            modelMetadataCache.set(modelName, metadata);
            return metadata;
        }
    } catch (err) {
        console.error(`Failed to show metadata for ${modelName}:`, err);
    }
    
    return null;
}

// Load Local Models
async function loadLocalModels() {
    try {
        // Fetch local tags
        const tagsResponse = await fetch('/api/tags');
        if (!tagsResponse.ok) {
            throw new Error('Failed to load local models.');
        }
        
        const tagsData = await tagsResponse.json();
        localModels = tagsData.models || [];
        
        // Update total models count
        statTotalModelsEl.textContent = String(localModels.length);

        // Warm up cache for all models
        await Promise.all(localModels.map(m => fetchModelMetadata(m.name)));

        // Fetch running models (VRAM)
        await updateVramUsageOnly();

        // Render Grid
        renderInstalledModels();
    } catch {
        installedModelsGrid.innerHTML = `
            <div class="empty-grid-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="var(--offline)" stroke-width="1.5" class="empty-icon">
                    <circle cx="12" cy="12" r="10"/>
                    <line x1="12" y1="8" x2="12" y2="12"/>
                    <line x1="12" y1="16" x2="12.01" y2="16"/>
                </svg>
                <p>Could not connect to Ollama. Make sure the Ollama service is running.</p>
            </div>
        `;
    }
}

// Separate VRAM fetcher for faster intervals
async function updateVramUsageOnly() {
    try {
        const psResponse = await fetch('/api/ps');
        if (psResponse.ok) {
            const psData = await psResponse.json();
            loadedModels = psData.models || [];
            
            let totalUsedVram = 0;
            if (loadedModels.length > 0) {
                const names = loadedModels.map(m => m.name.split(':')[0]).join(', ');
                statVramModelsEl.textContent = `${loadedModels.length} Loaded (${names})`;
                statVramModelsEl.className = 'stat-value text-online';
                btnUnloadAll.style.display = 'block';
                
                totalUsedVram = loadedModels.reduce((acc, m) => acc + (m.size_vram || 0), 0);
                vramLabelUsed.textContent = `${formatBytes(totalUsedVram)} Used`;
            } else {
                statVramModelsEl.textContent = 'None';
                statVramModelsEl.className = 'stat-value';
                btnUnloadAll.style.display = 'none';
                vramLabelUsed.textContent = '0 GB Used';
            }
            
            // Update VRAM stacked progress bar
            const loadedPercent = Math.min((totalUsedVram / gpuTotalVram) * 100, 100);
            vramBarLoaded.style.width = `${loadedPercent}%`;
            vramBarFree.style.width = `${100 - loadedPercent}%`;
            
            // Sync status indicators on cards
            document.querySelectorAll('.model-card').forEach(card => {
                const cardEl = /** @type {HTMLElement} */ (card);
                const modelName = cardEl.dataset.name;
                const isLoaded = loadedModels.some(m => m.name === modelName);
                
                const loadBtn = /** @type {HTMLButtonElement} */ (cardEl.querySelector('.load-btn'));
                const unloadBtn = /** @type {HTMLButtonElement} */ (cardEl.querySelector('.unload-btn'));
                
                if (isLoaded) {
                    cardEl.classList.add('active-model');
                    if (!cardEl.querySelector('.active-badge')) {
                        const header = cardEl.querySelector('.model-card-header');
                        const badge = document.createElement('span');
                        badge.className = 'active-badge';
                        badge.textContent = 'Active';
                        header.appendChild(badge);
                    }
                    if (unloadBtn) unloadBtn.style.display = 'flex';
                    if (loadBtn) loadBtn.style.display = 'none';
                } else {
                    cardEl.classList.remove('active-model');
                    const badge = cardEl.querySelector('.active-badge');
                    if (badge) badge.remove();
                    
                    if (unloadBtn) unloadBtn.style.display = 'none';
                    if (loadBtn) loadBtn.style.display = 'flex';
                }
            });
        }
    } catch (err) {
        console.error('Failed to retrieve active VRAM states:', err);
    }
}

// Render Installed Models Grid
function renderInstalledModels() {
    if (localModels.length === 0) {
        installedModelsGrid.innerHTML = `
            <div class="empty-grid-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" class="empty-icon">
                    <circle cx="12" cy="12" r="10"/>
                    <line x1="8" y1="12" x2="16" y2="12"/>
                </svg>
                <p>No models downloaded yet. Navigate to "Find & Download Models" to download your first model.</p>
            </div>
        `;
        return;
    }

    installedModelsGrid.innerHTML = '';
    
    localModels.forEach(model => {
        const isLoaded = loadedModels.some(m => m.name === model.name);
        
        const card = document.createElement('div');
        card.className = `model-card ${isLoaded ? 'active-model' : ''}`;
        card.dataset.name = model.name;
        
        const details = model.details || {};
        const quant = details.quantization_level || 'Unknown';
        const family = details.family || 'Unknown';
        const sizeFormatted = formatBytes(model.size);
        
        // Map Strengths
        const profile = getModelCapabilities(model.name, family);
        const tagsHtml = profile.tags.map(t => `<span class="strengths-tag" style="background-color: ${profile.color}">${t}</span>`).join('');
        
        card.innerHTML = `
            <div>
                <div class="model-card-header">
                    <h3 class="model-card-title">${model.name}</h3>
                    ${isLoaded ? '<span class="active-badge">Active</span>' : ''}
                </div>
                
                <div class="model-meta-info">
                    <div class="meta-row">
                        <span class="meta-label">File Size:</span>
                        <span class="meta-value">${sizeFormatted}</span>
                    </div>
                    <div class="meta-row">
                        <span class="meta-label">Family:</span>
                        <span class="meta-value mono">${family}</span>
                    </div>
                    <div class="meta-row">
                        <span class="meta-label">Quantization:</span>
                        <span class="meta-value mono">${quant}</span>
                    </div>
                </div>

                <!-- Strengths Section -->
                <div class="model-strengths-container">
                    <div class="strengths-title">Capabilities Profile</div>
                    <div class="strengths-tags-list">${tagsHtml}</div>
                    <p class="strengths-desc">${profile.desc}</p>
                </div>

                <!-- Dynamic VRAM Context Estimator -->
                <div class="estimator-toggle-row">
                    <button class="action-btn toggle-estimator-btn">
                        <span>VRAM & Context Calculator</span>
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="chevron-icon">
                            <polyline points="6 9 12 15 18 9"/>
                        </svg>
                    </button>
                </div>
                
                <div class="estimator-panel" style="display: none;">
                    <div class="estimator-slider-row">
                        <label>Context: <span class="slider-val">2,048</span> tokens</label>
                        <input type="range" min="512" max="16384" step="512" value="2048" class="ctx-slider">
                    </div>
                    <div class="estimator-metrics">
                        <div class="metric-item">
                            <span class="label">Weights:</span>
                            <span class="value weights-val">${sizeFormatted}</span>
                        </div>
                        <div class="metric-item">
                            <span class="label">KV Cache:</span>
                            <span class="value cache-val">0 B</span>
                        </div>
                        <div class="metric-item highlight">
                            <span class="label">Est VRAM:</span>
                            <span class="value total-est-val">0 B</span>
                        </div>
                    </div>
                    <div class="estimator-vram-bar">
                        <div class="est-bar-fill"></div>
                    </div>
                    <div class="est-status-msg">Fits in VRAM</div>
                </div>
            </div>
            
            <div class="model-card-actions">
                <button class="action-btn load-btn" style="display: ${isLoaded ? 'none' : 'flex'};">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="width:14px;height:14px;">
                        <polygon points="5 3 19 12 5 21 5 3"/>
                    </svg>
                    Load
                </button>
                <button class="action-btn unload-btn" style="display: ${isLoaded ? 'flex' : 'none'};">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="width:14px;height:14px;">
                        <path d="M18.36 6.64a9 9 0 1 1-12.73 0"/>
                        <line x1="12" y1="2" x2="12" y2="12"/>
                    </svg>
                    Unload
                </button>
                <button class="action-btn delete-btn">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="width:14px;height:14px;">
                        <polyline points="3 6 5 6 21 6"/>
                        <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/>
                    </svg>
                    Delete
                </button>
            </div>
        `;

        // Hooks for VRAM Estimator Slider
        const btnToggleEstimator = /** @type {HTMLButtonElement} */ (card.querySelector('.toggle-estimator-btn'));
        const panelEstimator = /** @type {HTMLElement} */ (card.querySelector('.estimator-panel'));
        const ctxSlider = /** @type {HTMLInputElement} */ (card.querySelector('.ctx-slider'));
        const sliderVal = /** @type {HTMLElement} */ (card.querySelector('.slider-val'));
        const cacheVal = /** @type {HTMLElement} */ (card.querySelector('.cache-val'));
        const totalEstVal = /** @type {HTMLElement} */ (card.querySelector('.total-est-val'));
        const estBarFill = /** @type {HTMLElement} */ (card.querySelector('.est-bar-fill'));
        const estStatusMsg = /** @type {HTMLElement} */ (card.querySelector('.est-status-msg'));

        // Load metadata parameters
        const metadata = modelMetadataCache.get(model.name);
        if (metadata) {
            ctxSlider.max = String(metadata.maxCtx || 16384);
        }

        const updateEstimator = () => {
            const ctxLen = Number(ctxSlider.value);
            sliderVal.textContent = ctxLen.toLocaleString();
            
            // Calculate KV cache VRAM
            let kvMultiplier = metadata ? metadata.kvCacheBytesPerToken : 65536; // fallback 64KB
            const cacheBytes = kvMultiplier * ctxLen;
            const totalEst = model.size + cacheBytes;
            
            cacheVal.textContent = formatBytes(cacheBytes);
            totalEstVal.textContent = formatBytes(totalEst);
            
            // Compare against total GPU VRAM
            // Subtract other currently loaded models size if they are active
            const otherLoadedVram = loadedModels
                .filter(m => m.name !== model.name)
                .reduce((acc, m) => acc + (m.size_vram || 0), 0);
            
            const totalRequired = totalEst + otherLoadedVram;
            const percent = Math.min((totalRequired / gpuTotalVram) * 100, 100);
            
            estBarFill.style.width = `${percent}%`;
            
            if (totalRequired <= gpuTotalVram) {
                estBarFill.className = 'est-bar-fill fits';
                estStatusMsg.className = 'est-status-msg fits';
                estStatusMsg.textContent = 'Fits in VRAM (100% GPU)';
            } else {
                estBarFill.className = 'est-bar-fill overflows';
                estStatusMsg.className = 'est-status-msg overflows';
                const overflowBytes = totalRequired - gpuTotalVram;
                estStatusMsg.textContent = `Overflows VRAM by ${formatBytes(overflowBytes)} (CPU split)`;
            }
        };

        btnToggleEstimator.addEventListener('click', () => {
            const isVisible = panelEstimator.style.display !== 'none';
            panelEstimator.style.display = isVisible ? 'none' : 'block';
            btnToggleEstimator.classList.toggle('expanded', !isVisible);
            if (!isVisible) {
                updateEstimator();
            }
        });

        ctxSlider.addEventListener('input', updateEstimator);

        // Load individual model handler (preload to memory)
        const btnLoad = /** @type {HTMLButtonElement} */ (card.querySelector('.load-btn'));
        btnLoad.addEventListener('click', async () => {
            btnLoad.disabled = true;
            btnLoad.textContent = 'Loading...';
            try {
                showToast(`Loading model ${model.name} into memory (pre-fetching weights)...`, 'info');
                // Sending empty prompt with indefinite keep_alive loads the model fully to GPU
                const response = await fetch('/api/generate', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ model: model.name, prompt: '', keep_alive: -1 })
                });
                
                if (response.ok) {
                    showToast(`${model.name} preloaded to VRAM successfully.`, 'success');
                } else {
                    throw new Error();
                }
            } catch {
                showToast('Failed to load model to memory.', 'error');
            } finally {
                btnLoad.disabled = false;
                btnLoad.innerHTML = `
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="width:14px;height:14px;">
                        <polygon points="5 3 19 12 5 21 5 3"/>
                    </svg> Load`;
                loadLocalModels();
            }
        });
        
        // Unload individual model handler
        const btnUnload = /** @type {HTMLButtonElement} */ (card.querySelector('.unload-btn'));
        btnUnload.addEventListener('click', async () => {
            btnUnload.disabled = true;
            try {
                showToast(`Unloading model ${model.name}...`, 'info');
                const response = await fetch('/api/generate', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ model: model.name, keep_alive: 0 })
                });
                
                if (response.ok) {
                    showToast(`${model.name} unloaded from VRAM.`, 'success');
                } else {
                    throw new Error();
                }
            } catch {
                showToast('Failed to unload model.', 'error');
            } finally {
                btnUnload.disabled = false;
                loadLocalModels();
            }
        });
        
        // Delete individual model handler
        const btnDelete = /** @type {HTMLButtonElement} */ (card.querySelector('.delete-btn'));
        btnDelete.addEventListener('click', async () => {
            if (confirm(`Are you sure you want to delete ${model.name}? This will permanently remove the model weights.`)) {
                btnDelete.disabled = true;
                btnDelete.textContent = 'Deleting...';
                try {
                    showToast(`Deleting ${model.name}...`, 'info');
                    const response = await fetch('/api/delete', {
                        method: 'DELETE',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ model: model.name })
                    });
                    
                    if (response.ok) {
                        showToast(`Successfully deleted ${model.name}`, 'success');
                        loadLocalModels();
                    } else {
                        const errText = await response.text();
                        throw new Error(errText);
                    }
                } catch (err) {
                    const errorVal = /** @type {Error} */ (err);
                    console.error(errorVal);
                    showToast(`Failed to delete model: ${errorVal.message || 'Unknown error'}`, 'error');
                    btnDelete.disabled = false;
                    btnDelete.textContent = 'Delete';
                }
            }
        });
        
        installedModelsGrid.appendChild(card);
    });
}

// Search Hugging Face
async function searchHuggingFace(query) {
    hfResultsContainer.innerHTML = `
        <div class="loading-state">
            <div class="spinner"></div>
            <p>Searching Hugging Face Hub for GGUF models...</p>
        </div>
    `;
    
    try {
        const response = await fetch(`/api/hf/search?q=${encodeURIComponent(query)}`);
        if (!response.ok) {
            throw new Error('Search request failed.');
        }
        
        hfSearchResults = await response.json();
        renderHfResults();
    } catch {
        hfResultsContainer.innerHTML = `
            <div class="empty-search-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="var(--offline)" stroke-width="1.5" class="empty-icon">
                    <circle cx="12" cy="12" r="10"/>
                    <line x1="12" y1="8" x2="12" y2="12"/>
                    <line x1="12" y1="16" x2="12.01" y2="16"/>
                </svg>
                <p>Failed to query Hugging Face API. Please check your internet connection.</p>
            </div>
        `;
    }
}

// Render Hugging Face Search Results
function renderHfResults() {
    if (hfSearchResults.length === 0) {
        hfResultsContainer.innerHTML = `
            <div class="empty-search-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" class="empty-icon">
                    <circle cx="12" cy="12" r="10"/>
                    <line x1="8" y1="12" x2="16" y2="12"/>
                </svg>
                <p>No matching GGUF repositories found. Try another query.</p>
            </div>
        `;
        return;
    }
    
    hfResultsContainer.innerHTML = '<div class="search-results-grid" id="hf-results-grid"></div>';
    const grid = document.getElementById('hf-results-grid');
    
    hfSearchResults.forEach(item => {
        const card = document.createElement('div');
        card.className = 'hf-card';
        
        const downloads = item.downloads ? item.downloads.toLocaleString() : '0';
        const likes = item.likes ? item.likes.toLocaleString() : '0';
        const shortDesc = `GGUF files for ${item.id.split('/').pop().replace(/-GGUF$/i, '')}`;
        
        card.innerHTML = `
            <div>
                <div class="hf-card-header">
                    <h4 class="hf-card-title">${item.id}</h4>
                    <p class="hf-card-author">by <span>${item.author || 'Anonymous'}</span></p>
                </div>
                
                <div class="hf-card-stats">
                    <div class="hf-stat">
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/>
                            <polyline points="7 10 12 15 17 10"/>
                            <line x1="12" y1="15" x2="12" y2="3"/>
                        </svg>
                        <span>${downloads}</span>
                    </div>
                    <div class="hf-stat">
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/>
                        </svg>
                        <span>${likes}</span>
                    </div>
                </div>
                
                <p class="hf-card-desc">${shortDesc}</p>
            </div>
            
            <button class="hf-card-action">Inspect Files</button>
        `;
        
        card.querySelector('.hf-card-action').addEventListener('click', () => {
            openHfModelModal(item);
        });
        
        grid.appendChild(card);
    });
}

// Open Hugging Face Repo details modal
async function openHfModelModal(repoItem) {
    modalSelectedRepoId = repoItem.id;
    modalSelectedFileSize = 0;
    
    modalRepoId.textContent = repoItem.id;
    modalAuthorName.textContent = repoItem.author || 'Anonymous';
    modalLikesCount.textContent = String(repoItem.likes || 0);
    
    modalFilesLoading.style.display = 'flex';
    modalFilesList.style.display = 'none';
    modalFilesList.innerHTML = '';
    modalCustomPullString.value = `hf.co/${repoItem.id}`;
    btnModalPullCustom.disabled = true;
    
    // Hide VRAM estimator until a GGUF file is selected
    modalEstimator.style.display = 'none';

    // Set strengths inside modal
    const profile = getModelCapabilities(repoItem.id, '');
    modalStrengthsBox.style.display = 'block';
    modalStrengthsTags.innerHTML = profile.tags.map(t => `<span class="strengths-tag" style="background-color: ${profile.color}">${t}</span>`).join('');
    modalStrengthsDesc.textContent = profile.desc;
    
    hfModelModal.classList.add('active');
    
    try {
        const response = await fetch(`/api/hf/model?repoId=${encodeURIComponent(repoItem.id)}`);
        if (!response.ok) throw new Error();
        
        const details = await response.json();
        const siblings = details.siblings || [];
        const ggufFiles = siblings.filter(s => s.rfilename && s.rfilename.endsWith('.gguf'));
        
        modalFilesLoading.style.display = 'none';
        modalFilesList.style.display = 'block';
        
        if (ggufFiles.length === 0) {
            modalFilesList.innerHTML = `
                <div style="padding:2rem;text-align:center;color:var(--text-muted);font-size:0.9rem;">
                    No .gguf files found in this repository.
                </div>
            `;
            return;
        }
        
        // Sorting files by name to group quantizations nicely
        ggufFiles.sort((a, b) => a.rfilename.localeCompare(b.rfilename));
        
        ggufFiles.forEach(file => {
            const row = document.createElement('div');
            row.className = 'file-row';
            
            const quantTag = extractQuantization(file.rfilename);
            const displayTag = quantTag || 'GGUF';
            const isRecommended = ['Q4_K_M', 'Q4_0', 'Q5_K_M', 'Q8_0'].includes(displayTag);
            
            const sizeInBytes = file.size || 0;
            const displaySize = sizeInBytes > 0 ? formatBytes(sizeInBytes) : 'Unknown Size';
            
            row.innerHTML = `
                <div class="file-info">
                    <span class="file-name">${file.rfilename}</span>
                    <span class="file-size-badge">${displaySize}</span>
                </div>
                <span class="quant-tag-badge ${isRecommended ? 'recommended' : ''}">
                    ${displayTag} ${isRecommended ? '★' : ''}
                </span>
            `;
            
            row.addEventListener('click', () => {
                document.querySelectorAll('.file-row').forEach(r => r.classList.remove('selected'));
                row.classList.add('selected');
                
                // Track sizes for visualizer calculations
                modalSelectedFileSize = sizeInBytes > 0 ? sizeInBytes : 4 * 1024 * 1024 * 1024; // fallback 4GB
                modalEstimator.style.display = 'block';
                updateModalVramEstimation();

                // Update pull identifier input
                if (quantTag) {
                    modalCustomPullString.value = `hf.co/${repoItem.id}:${quantTag.toLowerCase()}`;
                } else {
                    modalCustomPullString.value = `hf.co/${repoItem.id}`;
                }
                btnModalPullCustom.disabled = false;
            });
            
            modalFilesList.appendChild(row);
        });
        
    } catch {
        modalFilesLoading.style.display = 'none';
        modalFilesList.style.display = 'block';
        modalFilesList.innerHTML = `
            <div style="padding:2rem;text-align:center;color:var(--offline);font-size:0.9rem;">
                Failed to inspect repository files.
            </div>
        `;
    }
}

// Update Modal Estimator Panel
function updateModalVramEstimation() {
    const ctxLen = Number(modalCtxSlider.value);
    modalSliderVal.textContent = ctxLen.toLocaleString();

    const kvMultiplier = estimateKvCachePerToken(modalSelectedRepoId);
    const cacheBytes = kvMultiplier * ctxLen;
    const totalEst = modalSelectedFileSize + cacheBytes;

    modalWeightsVal.textContent = formatBytes(modalSelectedFileSize);
    modalCacheVal.textContent = formatBytes(cacheBytes);
    modalTotalEstVal.textContent = formatBytes(totalEst);

    // Sum currently loaded models VRAM
    const otherLoadedVram = loadedModels.reduce((acc, m) => acc + (m.size_vram || 0), 0);
    const totalRequired = totalEst + otherLoadedVram;
    const percent = Math.min((totalRequired / gpuTotalVram) * 100, 100);

    modalEstBarFill.style.width = `${percent}%`;

    if (totalRequired <= gpuTotalVram) {
        modalEstBarFill.className = 'est-bar-fill fits';
        modalEstStatusMsg.className = 'est-status-msg fits';
        modalEstStatusMsg.textContent = 'Fits in VRAM (100% GPU)';
    } else {
        modalEstBarFill.className = 'est-bar-fill overflows';
        modalEstStatusMsg.className = 'est-status-msg overflows';
        const overflowBytes = totalRequired - gpuTotalVram;
        modalEstStatusMsg.textContent = `Overflows VRAM by ${formatBytes(overflowBytes)} (CPU split)`;
    }
}

// Pull selected file from HF Modal
btnModalPullCustom.addEventListener('click', () => {
    const pullString = modalCustomPullString.value;
    if (pullString) {
        hfModelModal.classList.remove('active');
        pullModel(pullString);
    }
});

// Pull Model via streaming Ollama API
async function pullModel(modelName) {
    if (currentPullController) {
        showToast('A model download is already in progress. Please wait.', 'error');
        return;
    }

    currentPullController = new AbortController();
    
    // UI Init
    pullModelNameEl.textContent = modelName;
    pullStatusBadge.textContent = 'Connecting...';
    pullProgressPercent.textContent = '0%';
    pullProgressBytes.textContent = '0 B / 0 B';
    pullProgressFill.style.width = '0%';
    pullStatusLog.textContent = 'Initiating download stream...\n';
    
    progressDrawer.classList.add('active');
    showToast(`Downloading model ${modelName}...`, 'info');

    try {
        const response = await fetch('/api/pull', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ model: modelName }),
            signal: currentPullController.signal
        });

        if (!response.ok) {
            throw new Error(`Ollama returned status ${response.status}`);
        }

        const reader = response.body.getReader();
        const decoder = new TextDecoder();
        let buffer = '';

        while (true) {
            const { done, value } = await reader.read();
            if (done) break;

            buffer += decoder.decode(value, { stream: true });
            const lines = buffer.split('\n');
            buffer = lines.pop(); // save incomplete line in buffer

            for (const line of lines) {
                if (!line.trim()) continue;
                try {
                    const data = JSON.parse(line);
                    
                    // Update log and status badge
                    if (data.status) {
                        pullStatusBadge.textContent = data.status.split(' ')[0];
                        pullStatusLog.textContent = data.status + '\n' + pullStatusLog.textContent.substring(0, 500);
                    }

                    // Update progress bars
                    if (data.total > 0) {
                        const percent = Math.round((data.completed / data.total) * 100);
                        pullProgressPercent.textContent = `${percent}%`;
                        pullProgressBytes.textContent = `${formatBytes(data.completed)} / ${formatBytes(data.total)}`;
                        pullProgressFill.style.width = `${percent}%`;
                    }

                    // Success status
                    if (data.status === 'success') {
                        showToast(`Successfully pulled model: ${modelName}`, 'success');
                        pullStatusBadge.textContent = 'Completed';
                        pullProgressPercent.textContent = '100%';
                        pullProgressFill.style.width = '100%';
                        
                        setTimeout(() => {
                            progressDrawer.classList.remove('active');
                            currentPullController = null;
                            loadLocalModels();
                        }, 3000);
                        return;
                    }
                } catch (e) {
                    console.error('Failed to parse NDJSON line:', line, e);
                }
            }
        }
        
        // If stream ended but no success status was caught
        showToast(`Finished pull operations for: ${modelName}`, 'success');
        setTimeout(() => {
            progressDrawer.classList.remove('active');
            currentPullController = null;
            loadLocalModels();
        }, 2000);

    } catch (err) {
        const errorVal = /** @type {Error} */ (err);
        if (errorVal.name === 'AbortError') {
            showToast('Model download was cancelled.', 'info');
        } else {
            console.error('Error pulling model:', errorVal);
            showToast(`Download failed: ${errorVal.message || 'Server error'}`, 'error');
            pullStatusBadge.textContent = 'Failed';
            pullStatusLog.textContent = `Error: ${errorVal.message}\n` + pullStatusLog.textContent;
            
            setTimeout(() => {
                progressDrawer.classList.remove('active');
                currentPullController = null;
            }, 5000);
        }
    }
}
