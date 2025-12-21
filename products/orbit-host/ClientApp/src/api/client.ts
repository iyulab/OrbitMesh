import type { Agent, Job, Workflow, WorkflowInstance, ServerStatus, ApiToken, BootstrapToken, DeploymentProfile, DeploymentExecution, DeploymentStatusCounts, PagedResult } from '@/types'

const API_BASE = '/api'
const AUTH_STORAGE_KEY = 'orbitmesh_auth'

function getAuthHeader(): Record<string, string> {
  const password = sessionStorage.getItem(AUTH_STORAGE_KEY)
  if (password) {
    return { 'X-Admin-Password': password }
  }
  return {}
}

async function fetchApi<T>(endpoint: string, options?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE}${endpoint}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...getAuthHeader(),
      ...options?.headers,
    },
  })

  if (!response.ok) {
    const error = await response.text()
    throw new Error(error || `API error: ${response.status}`)
  }

  // Handle 204 No Content responses
  if (response.status === 204) {
    return undefined as T
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

// Bootstrap Token (TOFU enrollment) - Single reusable token
export async function getBootstrapToken(): Promise<BootstrapToken> {
  return fetchApi('/enrollment/bootstrap-token')
}

export async function regenerateBootstrapToken(): Promise<BootstrapToken> {
  return fetchApi('/enrollment/bootstrap-token/regenerate', {
    method: 'POST',
  })
}

export async function setBootstrapTokenEnabled(enabled: boolean): Promise<void> {
  await fetchApi('/enrollment/bootstrap-token/enabled', {
    method: 'PUT',
    body: JSON.stringify({ enabled }),
  })
}

export async function setBootstrapTokenAutoApprove(autoApprove: boolean): Promise<void> {
  await fetchApi('/enrollment/bootstrap-token/auto-approve', {
    method: 'PUT',
    body: JSON.stringify({ autoApprove }),
  })
}

// Deployment Profiles
export async function getDeploymentProfiles(): Promise<DeploymentProfile[]> {
  return fetchApi('/deployment/profiles')
}

export async function getDeploymentProfile(id: string): Promise<DeploymentProfile> {
  return fetchApi(`/deployment/profiles/${id}`)
}

export async function createDeploymentProfile(profile: Omit<DeploymentProfile, 'id' | 'createdAt' | 'lastDeployedAt'>): Promise<DeploymentProfile> {
  return fetchApi('/deployment/profiles', {
    method: 'POST',
    body: JSON.stringify(profile),
  })
}

export async function updateDeploymentProfile(id: string, profile: Partial<DeploymentProfile>): Promise<DeploymentProfile> {
  return fetchApi(`/deployment/profiles/${id}`, {
    method: 'PUT',
    body: JSON.stringify(profile),
  })
}

export async function deleteDeploymentProfile(id: string): Promise<void> {
  await fetchApi(`/deployment/profiles/${id}`, { method: 'DELETE' })
}

export async function triggerDeployment(profileId: string): Promise<DeploymentExecution> {
  return fetchApi(`/deployment/profiles/${profileId}/deploy`, {
    method: 'POST',
  })
}

export async function getMatchingAgents(profileId: string): Promise<Array<{ id: string; name: string }>> {
  return fetchApi(`/deployment/profiles/${profileId}/agents`)
}

// Deployment Executions
export async function getDeploymentExecutions(options?: {
  profileId?: string
  status?: string
  page?: number
  pageSize?: number
}): Promise<PagedResult<DeploymentExecution>> {
  const params = new URLSearchParams()
  if (options?.profileId) params.append('profileId', options.profileId)
  if (options?.status) params.append('status', options.status)
  if (options?.page) params.append('page', options.page.toString())
  if (options?.pageSize) params.append('pageSize', options.pageSize.toString())
  const query = params.toString() ? `?${params.toString()}` : ''
  return fetchApi(`/deployment/executions${query}`)
}

export async function getDeploymentExecution(id: string): Promise<DeploymentExecution> {
  return fetchApi(`/deployment/executions/${id}`)
}

export async function cancelDeployment(executionId: string): Promise<void> {
  await fetchApi(`/deployment/executions/${executionId}/cancel`, { method: 'POST' })
}

export async function getInProgressDeployments(): Promise<DeploymentExecution[]> {
  return fetchApi('/deployment/executions/in-progress')
}

export async function getDeploymentStatusCounts(): Promise<DeploymentStatusCounts> {
  return fetchApi('/deployment/status')
}
