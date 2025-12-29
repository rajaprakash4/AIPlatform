// Connect to the correct database
db = db.getSiblingDB('ai_platform');

// ============================================================
// 0. CLEANUP (Reset for fresh start)
// ============================================================
db.tool_definitions.drop();
db.agent_workflows.drop();

// ============================================================
// 1. DATA SERVICES (External API Integrations)
// ============================================================

// TOOL: Get_Employee_Details
// USAGE: Used to fetch profile data before making decisions.
db.tool_definitions.insertOne({
  "_id": "Get_Employee_Details",
  "Id": "Get_Employee_Details",
  "Type": "DataService",
  "Description": "Fetches authoritative employee profile data (Name, Role, Department, Base Salary, Tenure) from the HRIS system.",
  "InputKeys": ["EmployeeId"],
  "Configuration": {
    "Url": "https://jsonplaceholder.typicode.com/users/{EmployeeId}", // Replace with real internal API
    "Method": "GET",
    "TimeoutSeconds": 5,
    "CacheDurationMinutes": 60
  },
  "OutputAlias": {
    "name": "EmployeeName",
    "email": "WorkEmail",
    "website": "DepartmentCode" // Mapping dummy data for testing
  }
});

// ============================================================
// 2. KNOWLEDGE STORES (RAG / Vector DB)
// ============================================================

// TOOL: RAG_Tool
// USAGE: The primary research tool. Note the instruction to use 'StoreName'.
db.tool_definitions.insertOne({
  "_id": "RAG_Tool",
  "Id": "RAG_Tool",
  "Type": "KnowledgeStore",
  "Description": "Semantic search for company documents. REQUIRES a 'StoreName' to scope the search (e.g., 'HR', 'IT', 'Legal').",
  "InputKeys": ["Query", "StoreName"],
  "Configuration": {
    "CollectionPrefix": "enterprise_docs",
    "DefaultStore": "General",
    "TopK": 4, // Fetch top 4 chunks for better context
    "MinRelevanceScore": 0.75
  },
  "OutputAlias": {}
});

// ============================================================
// 3. AI SERVICES (Internal Logic / Utilities)
// ============================================================

// TOOL: Calculator
// USAGE: For precise math. LLMs are bad at math; this tool is the fix.
db.tool_definitions.insertOne({
  "_id": "Calculator",
  "Id": "Calculator",
  "Type": "AIService",
  "Description": "Executes mathematical expressions. Use this for salary adjustments, pro-rating bonuses, or tax calculations.",
  "InputKeys": ["expression"],
  "Configuration": {
    "AllowedOperations": ["+", "-", "*", "/", "(", ")", "^"],
    "Precision": 2
  },
  "OutputAlias": {
    "result": "CalculatedValue"
  }
});

// ============================================================
// 4. AGENTS (Workflows & Persona Instructions)
// ============================================================



// AGENT: HR_Agent
// THE SUPER AGENT: Handles People, Pay, and Policy.
db.agent_workflows.insertOne({
  "_id": "HR_Agent",
  "Name": "HR Specialist",
  "Description": "Handles employee inquiries regarding benefits, payroll, and company policy.",
  "Type": "Agent",
  "Tools": ["Get_Employee_Details", "RAG_Tool", "Calculator"],
  "Instructions": `
    You are a Senior HR Specialist. Your goal is to provide accurate, policy-backed answers.
    
    FOLLOW THIS PROCESS STRICTLY:
    1. CONTEXT: If the request involves a specific employee (e.g., "my bonus", "John's salary"), ALWAYS run 'Get_Employee_Details' first to get their Role and Department.
    2. POLICY: Use 'RAG_Tool' to find rules. 
       - CRITICAL: You MUST set 'StoreName' parameter to "HR" for this tool.
       - Search queries should be specific (e.g., "Senior Manager Bonus Policy 2024", "Remote work allowance engineering").
    3. CALCULATION: If the user asks for a number (bonus amount, tax, raise), DO NOT guess. Use the 'Calculator' tool with the data from Step 1 and Step 2.
    4. MISSING DATA: If the RAG tool returns no results, honestly state "I cannot find the HR policy document for this." and suggest the user upload it.
    
    TONE: Professional, empathetic, and confidential.
  `
});

// AGENT: Technical_Agent
// THE IT AGENT: Handles systems, login issues, and hardware.
db.agent_workflows.insertOne({
  "_id": "Technical_Agent",
  "Name": "IT Support Bot",
  "Description": "Assists with software installation, VPN access, and hardware troubleshooting.",
  "Type": "Agent",
  "Tools": ["RAG_Tool"], 
  "Instructions": `
    You are an IT Support Assistant.
    
    PROCESS:
    1. Search the knowledge base using 'RAG_Tool'.
    2. CRITICAL: Set 'StoreName' to "Technical" or "IT".
    3. If the solution involves complex steps, list them clearly as bullet points.
    4. If the document is missing, ask the user to upload the technical manual using the pin icon.
  `
});

print("✅ System initialized: HR Agent & Technical Agent are ready.");