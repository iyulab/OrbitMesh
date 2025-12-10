import type { Agent, Job, Workflow, WorkflowInstance, ServerStatus, ApiToken } from '@/types'

const API_BASE = '/api'

async function fetchApi<T>(endpoint: string, options?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE}${endpoint}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...options?.headers,
    },
  })

  if (!response.ok) {
    const error = await response.text()
    throw new Error(error || `API error: ${response.status}`)
  }

  return response.json()
}

// Server
export async function getServerStatus(): Promise<ServerStatus> {
  return fetchApi('/status')
}

// Agents
export async function getAgents(): Promise<Agent[]> {
  return fetchApi('/agents')
}

export async function getAgent(id: string): Promise<Agent> {
  return fetchApi(`/agents/${id}`)
}

// Jobs
export async function getJobs(status?: string): Promise<Job[]> {
  const query = status ? `?status=${status}` : ''
  return fetchApi(`/jobs${query}`)
}

export async function getJob(id: string): Promise<Job> {
  return fetchApi(`/jobs/${id}`)
}

export async function submitJob(request: {
  command: string
  pattern?: string
  payload?: object
  priority?: number
  requiredCapabilities?: string[]
  requiredTags?: string[]
  timeout?: string
}): Promise<Job> {
  return fetchApi('/jobs', {
    method: 'POST',
    body: JSON.stringify(request),
  })
}

export async function cancelJob(id: string): Promise<void> {
  await fetchApi(`/jobs/${id}/cancel`, { method: 'POST' })
}

// Workflows
export async function getWorkflows(): Promise<Workflow[]> {
  return fetchApi('/workflows')
}

export async function getWorkflow(id: string): Promise<Workflow> {
  return fetchApi(`/workflows/${id}`)
}

export async function getWorkflowInstances(workflowId?: string): Promise<WorkflowInstance[]> {
  const query = workflowId ? `?workflowId=${workflowId}` : ''
  return fetchApi(`/workflows/instances${query}`)
}

export async function startWorkflow(workflowId: string, input?: Record<string, unknown>): Promise<WorkflowInstance> {
  return fetchApi(`/workflows/${workflowId}/start`, {
    method: 'POST',
    body: JSON.stringify({ input }),
  })
}

export async function createWorkflow(workflow: {
  name: string
  description?: string
  version?: string
  steps: Array<{
    id: string
    type: string
    name?: string
    config: Record<string, unknown>
  }>
}): Promise<Workflow> {
  return fetchApi('/workflows', {
    method: 'POST',
    body: JSON.stringify(workflow),
  })
}

export async function updateWorkflow(id: string, workflow: {
  name: string
  description?: string
  version?: string
  isActive?: boolean
  steps: Array<{
    id: string
    type: string
    name?: string
    config: Record<string, unknown>
  }>
}): Promise<Workflow> {
  return fetchApi(`/workflows/${id}`, {
    method: 'PUT',
    body: JSON.stringify(workflow),
  })
}

export async function duplicateWorkflow(id: string, newName?: string): Promise<Workflow> {
  // Get the original workflow
  const original = await getWorkflow(id)

  // Create a copy with a new name
  return createWorkflow({
    name: newName || `${original.name} (Copy)`,
    description: original.description,
    version: '1.0.0',
    steps: original.steps.map((step) => ({
      id: `${step.id}_copy_${Date.now()}`,
      type: step.type,
      name: step.name,
      config: step.config,
    })),
  })
}

export async function deleteWorkflow(id: string): Promise<void> {
  await fetchApi(`/workflows/${id}`, { method: 'DELETE' })
}

// API Tokens (for agent registration)
export async function getApiTokens(): Promise<ApiToken[]> {
  return fetchApi('/tokens')
}

export async function createApiToken(request: {
  name: string
  scopes?: string[]
  expiresInDays?: number
}): Promise<ApiToken> {
  return fetchApi('/tokens', {
    method: 'POST',
    body: JSON.stringify(request),
  })
}

export async function revokeApiToken(id: string): Promise<void> {
  await fetchApi(`/tokens/${id}`, { method: 'DELETE' })
}

// Agent connection command generator (like GPUStack)
export function generateAgentCommand(serverUrl: string, token: string, options?: {
  name?: string
  group?: string
  tags?: string[]
}): string {
  const args = [
    'orbit-agent',
    `--server-url "${serverUrl}"`,
    `--token "${token}"`,
  ]

  if (options?.name) {
    args.push(`--name "${options.name}"`)
  }
  if (options?.group) {
    args.push(`--group "${options.group}"`)
  }
  if (options?.tags?.length) {
    args.push(`--tags "${options.tags.join(',')}"`)
  }

  return args.join(' \\\n  ')
}

export function generateDockerCommand(serverUrl: string, token: string, options?: {
  name?: string
  group?: string
  tags?: string[]
  image?: string
}): string {
  const image = options?.image || 'orbitmesh/agent:latest'
  const envVars = [
    `-e ORBIT_SERVER_URL="${serverUrl}"`,
    `-e ORBIT_TOKEN="${token}"`,
  ]

  if (options?.name) {
    envVars.push(`-e ORBIT_AGENT_NAME="${options.name}"`)
  }
  if (options?.group) {
    envVars.push(`-e ORBIT_AGENT_GROUP="${options.group}"`)
  }
  if (options?.tags?.length) {
    envVars.push(`-e ORBIT_AGENT_TAGS="${options.tags.join(',')}"`)
  }

  return `docker run -d \\\n  ${envVars.join(' \\\n  ')} \\\n  ${image}`
}
