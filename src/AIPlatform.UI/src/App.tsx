import React, { useState, useEffect, useRef, Component } from 'react';
import { 
  Box, TextField, IconButton, Paper, Typography, Avatar, 
  Button, Dialog, DialogTitle, DialogContent, 
  DialogActions, List, ListItem, ListItemButton, ListItemIcon, 
  ListItemText, Checkbox, Chip, Collapse, CircularProgress, Card, 
  Alert
} from '@mui/material';
import { 
  Send as SendIcon, SmartToy as BotIcon, Person as UserIcon, 
  Settings as SettingsIcon, Build as ToolIcon, Hub as AgentIcon,
  ExpandMore as ExpandIcon, CheckCircle as CheckIcon, Pending as PendingIcon,
  Error as ErrorIcon, CloudUpload as UploadIcon, AttachFile as AttachIcon,
  Description as DocIcon
} from '@mui/icons-material';
import ReactMarkdown from 'react-markdown';

// ⚠️ CONFIRM YOUR API PORT HERE
const API_BASE_URL = "https://localhost:7000"; 

// ==========================================
// 1. ERROR BOUNDARY (Prevents White Screen Crashes)
// ==========================================
class SafeErrorBoundary extends Component<{ children: React.ReactNode }, { hasError: boolean, error: string }> {
  constructor(props: any) { super(props); this.state = { hasError: false, error: "" }; }
  static getDerivedStateFromError(error: any) { return { hasError: true, error: error.toString() }; }
  render() { 
    if (this.state.hasError) return (
        <Box sx={{ p: 4, textAlign: 'center' }}>
            <ErrorIcon color="error" sx={{ fontSize: 60 }} />
            <Typography variant="h6" color="error">UI Crash</Typography>
            <Typography variant="caption" display="block" sx={{ fontFamily: 'monospace', mt:1 }}>{this.state.error}</Typography>
            <Button sx={{ mt: 2 }} onClick={() => window.location.reload()}>Reload</Button>
        </Box>
    );
    return this.props.children; 
  }
}

// ==========================================
// 2. TYPE DEFINITIONS
// ==========================================
interface PlanStep { 
    stepId: number; 
    toolId: string; 
    description: string; 
    status: 'pending' | 'running' | 'completed' | 'waiting_for_user' | 'failed';
    result?: any; 
    inputMapping?: any; 
    validationError?: string; 
}

interface Message { 
    id: string; 
    sender: 'user' | 'ai'; 
    text: string; 
    plan?: PlanStep[]; 
    isThinkingOpen?: boolean; 
    isError?: boolean; 
}

interface PersonaOption { 
    id: string; label: string; type: 'Agent' | 'Tool'; description: string; 
}

// ==========================================
// 3. MAIN APP LOGIC
// ==========================================
function AppContent() {
  const [input, setInput] = useState('');
  const [messages, setMessages] = useState<Message[]>([
    { id: '1', sender: 'ai', text: 'Hello! I am your Enterprise Assistant. Upload documents using the 📎 pin, or ask me anything.' }
  ]);
  
  // Skill Config State
  const [personas, setPersonas] = useState<PersonaOption[]>([]);
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [isSkillModalOpen, setIsSkillModalOpen] = useState(false);
  
  // Upload State
  const [isUploadDialogOpen, setIsUploadDialogOpen] = useState(false);
  const [pendingFile, setPendingFile] = useState<File | null>(null);
  const [uploadStoreName, setUploadStoreName] = useState("General");
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [isTyping, setIsTyping] = useState(false);
  const messagesEndRef = useRef<null | HTMLDivElement>(null);

  // --- INITIAL LOAD ---
  useEffect(() => {
    const fetchData = async () => {
        try {
            const [tRes, wRes] = await Promise.allSettled([
                fetch(`${API_BASE_URL}/api/Agent/tools`),
                fetch(`${API_BASE_URL}/api/Agent/workflows`)
            ]);
            let merged: PersonaOption[] = [];
            if (wRes.status === 'fulfilled' && wRes.value.ok) {
                const data = await wRes.value.json();
                merged = merged.concat(data.map((w: any) => ({ id: w.id, label: w.id, type: 'Agent', description: w.description })));
            }
            if (tRes.status === 'fulfilled' && tRes.value.ok) {
                const data = await tRes.value.json();
                merged = merged.concat(data.map((t: any) => ({ id: t.id, label: t.id, type: 'Tool', description: t.description })));
            }
            setPersonas(merged);
        } catch (e) { console.error("Init Error", e); }
    };
    fetchData();
  }, []);

  // ==========================================
  // 4. UPLOAD LOGIC (PIN ICON & ERROR RECOVERY)
  // ==========================================
  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
      if (e.target.files && e.target.files[0]) {
          setPendingFile(e.target.files[0]);
          setIsUploadDialogOpen(true); 
          e.target.value = ''; // Reset input so same file can be selected again
      }
  };

  const confirmUpload = async () => {
      if (!pendingFile) return;

      const formData = new FormData();
      formData.append('file', pendingFile);

      try {
          // Optimistic UI
          setMessages(p => [...p, { 
              id: Date.now().toString(), 
              sender: 'ai', 
              text: `Ingesting **${pendingFile.name}** into store **${uploadStoreName}**... ⏳` 
          }]);

          // Call DocumentController
          const res = await fetch(`${API_BASE_URL}/api/Document/upload?storeName=${encodeURIComponent(uploadStoreName)}`, {
              method: 'POST',
              body: formData
          });

          if (!res.ok) throw new Error(await res.text() || "Upload failed");
          
          const result = await res.json();
          setMessages(p => [...p, { 
              id: Date.now().toString(), 
              sender: 'ai', 
              text: `✅ **Success!** Indexed **${pendingFile.name}** into **${uploadStoreName}**.\nDoc ID: \`${result.docId}\`` 
          }]);

      } catch (e: any) {
          setMessages(p => [...p, { id: Date.now().toString(), sender: 'ai', text: `❌ Upload Failed: ${e.message}`, isError: true }]);
      } finally {
          setIsUploadDialogOpen(false);
          setPendingFile(null);
      }
  };

  // ==========================================
  // 5. CHAT & EXECUTION LOGIC
  // ==========================================
  
  // Helper: Call Validation API
  const validateInput = async (toolId: string, key: string, value: string): Promise<{isValid: boolean, error?: string}> => {
      try {
          const res = await fetch(`${API_BASE_URL}/api/Validation`, {
              method: 'POST', headers: {'Content-Type': 'application/json'},
              body: JSON.stringify({ toolId, inputKey: key, inputValue: String(value), description: key })
          });
          return await res.json();
      } catch { return { isValid: true }; } 
  };

  const handleSend = async () => {
    if (!input.trim()) return;
    const userMsg: Message = { id: Date.now().toString(), sender: 'user', text: input };
    setMessages(prev => [...prev, userMsg]);
    setInput('');
    setIsTyping(true);

    try {
        const res = await fetch(`${API_BASE_URL}/api/Router/plan`, {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ userMessage: userMsg.text, allowedIds: selectedIds })
        });
        
        const planData = await res.json();
        const steps: PlanStep[] = (planData.steps || []).map((s:any) => ({ ...s, status: 'pending', inputMapping: s.inputMapping || {} }));

        const aiMsg: Message = { 
            id: (Date.now() + 1).toString(), sender: 'ai', 
            text: `**Goal:** ${planData.goal || "Processing..."}`, plan: steps, isThinkingOpen: true 
        };
        setMessages(prev => [...prev, aiMsg]);
        executePlan(aiMsg.id, steps);

    } catch (e: any) { 
        setMessages(prev => [...prev, { id: Date.now().toString(), sender: 'ai', text: `❌ **Error:** ${e.message}`, isError: true }]);
        setIsTyping(false); 
    }
  };

  const executePlan = async (msgId: string, steps: PlanStep[], accumulatedData: any = {}) => {
    const newSteps = [...steps];
    
    for (let i = 0; i < newSteps.length; i++) {
        const step = newSteps[i];
        if (step.status === 'completed' || step.status === 'failed') continue;

        // 1. Resolve Inputs
        const resolvedInputs: any = {};
        let missingUser = false;
        
        for (const [k, v] of Object.entries(step.inputMapping || {})) {
            const valStr = String(v);
            if (valStr === '{{USER_INPUT}}') {
                missingUser = true;
            } else if (valStr.startsWith('{{Step')) {
                try {
                    const parts = valStr.replace('{{','').replace('}}','').split('.');
                    if (parts.length >= 1) {
                        const sourceId = parseInt(parts[0].replace('Step',''));
                        const sourceData = accumulatedData[sourceId];
                        resolvedInputs[k] = (parts.length > 2 && sourceData) ? (sourceData[parts[2]] || sourceData) : sourceData;
                    }
                } catch { console.warn(`Mapping failed for ${valStr}`); }
            } else {
                resolvedInputs[k] = v;
            }
        }

        // 2. AUTO-VALIDATION (Check AI extracted values)
        if (!missingUser) {
            for (const [key, val] of Object.entries(resolvedInputs)) {
                if (typeof val === 'string' && val.length < 100) {
                    const check = await validateInput(step.toolId, key, val as string);
                    if (!check.isValid) {
                        missingUser = true; 
                        newSteps[i].inputMapping[key] = '{{USER_INPUT}}'; 
                        newSteps[i].validationError = `AI proposed '${val}' but it was invalid: ${check.error}`;
                        break; 
                    }
                }
            }
        }

        // 3. Pause for User?
        if (missingUser) {
            newSteps[i].status = 'waiting_for_user';
            updateMsg(msgId, newSteps);
            setIsTyping(false);
            return; 
        }

        // 4. Run Tool
        newSteps[i].status = 'running';
        newSteps[i].validationError = undefined;
        updateMsg(msgId, newSteps);
        
        try {
            const endpoint = `/api/Agent/tool/run/${step.toolId}`;
            const res = await fetch(`${API_BASE_URL}${endpoint}`, {
                method: 'POST', headers: {'Content-Type': 'application/json'},
                body: JSON.stringify(resolvedInputs)
            });
            const result = await res.json();
            
            newSteps[i].status = 'completed';
            newSteps[i].result = result;
            accumulatedData[step.stepId] = result.finalContext || result;
            updateMsg(msgId, newSteps);
        } catch (e) {
            newSteps[i].status = 'failed';
            updateMsg(msgId, newSteps);
            break; 
        }
    }
    setIsTyping(false);
  };

  const updateMsg = (id: string, steps: PlanStep[]) => setMessages(p => p.map(m => m.id === id ? { ...m, plan: steps } : m));

  const handleUserSubmit = async (msgId: string, stepId: number, data: any) => {
      const msg = messages.find(m => m.id === msgId);
      if(!msg || !msg.plan) return;

      const newPlan = msg.plan.map(s => 
          s.stepId === stepId ? { ...s, inputMapping: { ...s.inputMapping, ...data }, validationError: undefined } : s
      );

      const accData: any = {};
      newPlan.forEach(s => { if(s.status === 'completed') accData[s.stepId] = s.result; });

      updateMsg(msgId, newPlan as PlanStep[]);
      executePlan(msgId, newPlan as PlanStep[], accData);
  };

  useEffect(() => { messagesEndRef.current?.scrollIntoView({ behavior: "smooth" }); }, [messages]);

  // ==========================================
  // 6. RENDER
  // ==========================================
  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', height: '100vh', bgcolor: '#fff' }}>
      
      {/* HEADER */}
      <Box sx={{ p: 2, borderBottom: '1px solid #eee', bgcolor: '#f8f9fa', display: 'flex', justifyContent: 'space-between' }}>
         <Box sx={{display: 'flex', alignItems: 'center', gap: 2}}>
             <Typography variant="h6" fontWeight="bold">Enterprise AI</Typography>
             <Chip 
                label={selectedIds.length === 0 ? "All Skills" : `${selectedIds.length} Restricted`} 
                color={selectedIds.length === 0 ? "success" : "primary"} variant={selectedIds.length === 0 ? "filled" : "outlined"}
                icon={selectedIds.length === 0 ? <BotIcon sx={{fontSize: 16}}/> : <SettingsIcon sx={{fontSize: 16}}/>}
                onClick={() => setIsSkillModalOpen(true)}
                sx={{ cursor: 'pointer', fontWeight: 'bold' }}
             />
         </Box>
         <Button size="small" startIcon={<SettingsIcon />} onClick={() => setIsSkillModalOpen(true)}>Skills</Button>
      </Box>

      {/* CHAT AREA */}
      <Box sx={{ flexGrow: 1, overflowY: 'auto', p: 3, display: 'flex', flexDirection: 'column', gap: 3 }}>
        {messages.map((msg) => (
           <Box key={msg.id} sx={{ display: 'flex', gap: 2, flexDirection: msg.sender === 'user' ? 'row-reverse' : 'row' }}>
              <Avatar sx={{ bgcolor: msg.sender === 'ai' ? (msg.isError ? '#d32f2f' : '#1976d2') : '#333' }}>
                  {msg.isError ? <ErrorIcon /> : (msg.sender === 'ai' ? <BotIcon/> : <UserIcon/>)}
              </Avatar>
              <Box sx={{ maxWidth: '85%', minWidth: '300px' }}>
                  <Paper sx={{ 
                      p: 2, borderRadius: 2, 
                      bgcolor: msg.isError ? '#ffebee' : (msg.sender === 'ai' ? '#f5f7f9' : '#e3f2fd'),
                      color: msg.isError ? '#c62828' : '#1f1f1f'
                  }}>
                      <ReactMarkdown>{msg.text}</ReactMarkdown>
                  </Paper>

                  {/* EXECUTION PLAN */}
                  {msg.sender === 'ai' && msg.plan && msg.plan.length > 0 && (
                      <Card variant="outlined" sx={{ mt: 1.5, borderColor: '#e0e0e0', overflow: 'hidden' }}>
                          <Box 
                            sx={{ p: 1.5, bgcolor: '#fafafa', cursor: 'pointer', display: 'flex', justifyContent: 'space-between' }}
                            onClick={() => setMessages(p => p.map(m => m.id === msg.id ? { ...m, isThinkingOpen: !m.isThinkingOpen } : m))}
                          >
                             <Box sx={{display:'flex', gap:1, alignItems:'center'}}>
                                <Typography variant="caption" fontWeight="bold" sx={{ color: '#666' }}>EXECUTION PLAN ({msg.plan.length} Steps)</Typography>
                                {msg.plan.some(s => s.status === 'failed') ? <Chip label="Failed" size="small" color="error" sx={{height:16}}/> :
                                 msg.plan.every(s => s.status === 'completed') ? <Chip label="Complete" size="small" color="success" sx={{height:16}}/> : 
                                 <Chip label="Running" size="small" color="primary" sx={{height:16}}/>}
                             </Box>
                             <ExpandIcon sx={{ transform: msg.isThinkingOpen ? 'rotate(180deg)' : 'none', color: '#999' }} />
                          </Box>

                          <Collapse in={msg.isThinkingOpen}>
                              <Box>
                                  {msg.plan.map((step) => (
                                      <Box key={step.stepId} sx={{ 
                                          p: 2, borderBottom: '1px solid #f0f0f0',
                                          bgcolor: step.status === 'running' ? '#f0f7ff' : (step.status === 'failed' ? '#fff5f5' : 'transparent')
                                      }}>
                                          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
                                              {step.status === 'completed' ? <CheckIcon color="success" fontSize="small"/> : 
                                               step.status === 'failed' ? <ErrorIcon color="error" fontSize="small"/> :
                                               step.status === 'running' ? <CircularProgress size={16}/> : 
                                               <PendingIcon color="disabled" fontSize="small"/>}
                                              <Typography variant="body2" fontWeight="bold">{step.toolId}</Typography>
                                          </Box>
                                          <Typography variant="caption" color="text.secondary" sx={{ ml: 3.5, display: 'block', mb: 1 }}>{step.description}</Typography>

                                          {/* INPUT FORM (Waiting for User) */}
                                          {step.status === 'waiting_for_user' && (
                                              <Paper variant="outlined" sx={{ ml: 3.5, p: 2, borderColor: '#ed6c02', bgcolor: '#fff8e1' }}>
                                                  <Typography variant="caption" color="warning.main" fontWeight="bold" sx={{mb:1, display:'block'}}>⚠️ INPUT REQUIRED</Typography>
                                                  {step.validationError && <Alert severity="error" sx={{ mb: 1, py: 0 }}>{step.validationError}</Alert>}
                                                  {Object.entries(step.inputMapping || {}).filter(([_,v]) => String(v) === '{{USER_INPUT}}').map(([k]) => (
                                                      <TextField 
                                                        key={k} size="small" fullWidth placeholder={`Enter ${k}...`} sx={{ bgcolor: 'white' }} 
                                                        onKeyDown={(e) => {
                                                            if(e.key === 'Enter') handleUserSubmit(msg.id, step.stepId, { [k]: (e.target as HTMLInputElement).value });
                                                        }}
                                                      />
                                                  ))}
                                              </Paper>
                                          )}

                                          {/* MISSING DATA HANDLER (Red Box) */}
                                          {step.status === 'completed' && step.result && step.result.isMissingData && (
                                              <Paper variant="outlined" sx={{ ml: 3.5, p: 2, borderColor: '#d32f2f', bgcolor: '#fff5f5' }}>
                                                  <Box sx={{ display: 'flex', gap: 2, alignItems: 'flex-start' }}>
                                                      <ErrorIcon color="error" sx={{ mt: 0.5 }} />
                                                      <Box sx={{ flexGrow: 1 }}>
                                                          <Typography variant="subtitle2" color="error" fontWeight="bold">Knowledge Gap Detected</Typography>
                                                          <Typography variant="body2" sx={{ mb: 2 }}>{step.result.message || "I don't have this document yet."}</Typography>
                                                          <Button variant="contained" color="error" size="small" startIcon={<UploadIcon />}
                                                              onClick={() => {
                                                                  setUploadStoreName(step.result.targetStore || "General");
                                                                  fileInputRef.current?.click();
                                                              }}
                                                          >
                                                              Upload to {step.result.targetStore || "Store"}
                                                          </Button>
                                                      </Box>
                                                  </Box>
                                              </Paper>
                                          )}

                                          {/* SMART RESULT RENDERER (Table or Text) */}
                                          {step.status === 'completed' && step.result && !step.result.isMissingData && (
                                              <Box sx={{ ml: 3.5, mt: 1 }}>
                                                  {step.result.RAG_Results ? (
                                                      // RAG TEXT VIEW
                                                      <Paper variant="outlined" sx={{ p: 2, bgcolor: '#fbfdfe', borderColor: '#d0e3f0' }}>
                                                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1.5, borderBottom: '1px solid #e1f5fe', pb: 1 }}>
                                                            <DocIcon color="primary" fontSize="small" />
                                                            <Typography variant="caption" fontWeight="bold" color="primary">
                                                                FOUND IN {step.result.targetStore || "KNOWLEDGE BASE"}
                                                            </Typography>
                                                        </Box>
                                                        
                                                        <Box sx={{ 
                                                            maxHeight: '300px', 
                                                            overflowY: 'auto', 
                                                            fontSize: '0.9rem', 
                                                            color: '#2c3e50',
                                                            '& p': { mb: 1 }, // Add spacing between paragraphs
                                                            '& ul': { pl: 2 }, // Indent lists
                                                            '& hr': { borderColor: '#eee', my: 2 } // Style the dividers
                                                        }}>
                                                            <ReactMarkdown>
                                                                {step.result.RAG_Results}
                                                            </ReactMarkdown>
                                                        </Box>
                                                    </Paper>
                                                  ) : (
                                                      // DATA TABLE VIEW
                                                      <Paper variant="outlined" sx={{ overflow: 'hidden', borderColor: '#e0e0e0' }}>
                                                          <Box sx={{ bgcolor: '#f5f5f5', px: 2, py: 0.5, borderBottom: '1px solid #e0e0e0' }}>
                                                              <Typography variant="caption" fontWeight="bold" color="text.secondary">OUTPUT DATA</Typography>
                                                          </Box>
                                                          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.85rem' }}>
                                                              <tbody>
                                                                  {Object.entries(step.result).map(([key, value]) => (
                                                                      !['success', 'isMissingData', 'targetStore'].includes(key) && (
                                                                          <tr key={key} style={{ borderBottom: '1px solid #f0f0f0' }}>
                                                                              <td style={{ padding: '8px', width: '30%', fontWeight: 600, color: '#555', backgroundColor: '#fafafa' }}>{key}</td>
                                                                              <td style={{ padding: '8px', color: '#000', fontFamily: 'monospace' }}>
                                                                                  {typeof value === 'object' ? JSON.stringify(value) : String(value)}
                                                                              </td>
                                                                          </tr>
                                                                      )
                                                                  ))}
                                                              </tbody>
                                                          </table>
                                                      </Paper>
                                                  )}
                                              </Box>
                                          )}
                                      </Box>
                                  ))}
                              </Box>
                          </Collapse>
                      </Card>
                  )}
              </Box>
           </Box>
        ))}
        {isTyping && <Box sx={{display:'flex', gap:1, ml: 6}}><CircularProgress size={16} /><Typography variant="caption">AI is thinking...</Typography></Box>}
        <div ref={messagesEndRef} />
      </Box>

      {/* INPUT AREA */}
      <Box sx={{ p: 2, borderTop: '1px solid #eee', bgcolor: '#fff', display: 'flex', gap: 1 }}>
          <input type="file" hidden ref={fileInputRef} onChange={handleFileSelect} />
          <IconButton color="primary" onClick={() => fileInputRef.current?.click()}><AttachIcon /></IconButton>
          <TextField 
            fullWidth placeholder="Message..." value={input} onChange={e => setInput(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && handleSend()} disabled={isTyping}
            sx={{ '& .MuiOutlinedInput-root': { borderRadius: 3, bgcolor: '#f8f9fa' } }}
          />
          <IconButton color="primary" onClick={handleSend} disabled={!input || isTyping}><SendIcon /></IconButton>
      </Box>

      {/* SKILL MODAL */}
      <Dialog open={isSkillModalOpen} onClose={() => setIsSkillModalOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle sx={{borderBottom: '1px solid #eee'}}>Select Active Skills</DialogTitle>
        <DialogContent sx={{ p: 0 }}>
            {personas.length === 0 ? <Box p={3} textAlign="center"><CircularProgress size={20}/></Box> : 
            <List>
                {personas.map((p) => (
                    <ListItem key={p.id} disablePadding divider>
                        <ListItemButton onClick={() => setSelectedIds(prev => prev.includes(p.id) ? prev.filter(x=>x!==p.id) : [...prev, p.id])}>
                            <ListItemIcon><Checkbox edge="start" checked={selectedIds.includes(p.id)} disableRipple /></ListItemIcon>
                            <ListItemText primary={p.label} secondary={p.description} />
                            <Chip label={p.type} size="small" sx={{ fontSize: '0.6rem', height: 20 }} />
                        </ListItemButton>
                    </ListItem>
                ))}
            </List>}
        </DialogContent>
        <DialogActions sx={{ p: 2, borderTop: '1px solid #eee' }}>
            <Button onClick={() => setSelectedIds([])} color="error">Reset (All)</Button>
            <Button onClick={() => setIsSkillModalOpen(false)} variant="contained">Done</Button>
        </DialogActions>
      </Dialog>

      {/* UPLOAD STORE DIALOG */}
      <Dialog open={isUploadDialogOpen} onClose={() => setIsUploadDialogOpen(false)}>
        <DialogTitle>Ingest Document</DialogTitle>
        <DialogContent>
            <Typography variant="body2" sx={{ mb: 2, color: '#666' }}>
                Uploading <b>{pendingFile?.name}</b>. Which Knowledge Store should this belong to?
            </Typography>
            <TextField 
                autoFocus fullWidth label="Store Name (e.g. HR, IT)" 
                variant="outlined" value={uploadStoreName} onChange={e => setUploadStoreName(e.target.value)}
            />
        </DialogContent>
        <DialogActions sx={{p: 2}}>
            <Button onClick={() => setIsUploadDialogOpen(false)} color="inherit">Cancel</Button>
            <Button onClick={confirmUpload} variant="contained" startIcon={<UploadIcon />}>Ingest</Button>
        </DialogActions>
      </Dialog>

    </Box>
  );
}

export default function App() { return <SafeErrorBoundary><AppContent /></SafeErrorBoundary>; }