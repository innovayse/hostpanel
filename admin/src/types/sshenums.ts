/**
 * The closed value sets of SSH key management, as the API sends them (the enum member names). The
 * members are the exact strings, so a comparison reads `result.error === SshProviderFailure.Unreachable`.
 */

/** Why an SSH key agent call failed. */
export enum SshProviderFailure {
  /** The agent answered with a reason this version does not know. */
  Unknown = 'Unknown',
  /** The server could not be reached or the connection dropped. */
  Unreachable = 'Unreachable',
  /** The call timed out. */
  Timeout = 'Timeout',
  /** The agent failed internally or answered something unreadable. */
  InternalError = 'InternalError',
  /** The agent speaks another contract version. */
  UnsupportedVersion = 'UnsupportedVersion',
  /** The agent rejected the request as malformed. */
  InvalidRequest = 'InvalidRequest',
  /** The agent does not know the requested operation. */
  UnknownOperation = 'UnknownOperation',
  /** The request exceeded the agent's size limit. */
  RequestTooLarge = 'RequestTooLarge',
  /** The account name is not valid. */
  InvalidAccount = 'InvalidAccount',
  /** The account does not exist on the server. */
  AccountNotFound = 'AccountNotFound',
  /** The account may not have managed keys. */
  AccountNotEligible = 'AccountNotEligible',
  /** Shell access is disabled for the account. */
  SshDisabled = 'SshDisabled',
  /** The public key is not valid. */
  InvalidKey = 'InvalidKey',
  /** The key is already installed. */
  DuplicateKey = 'DuplicateKey',
  /** The key id is already used by a different key. */
  KeyIdConflict = 'KeyIdConflict',
  /** The account reached its key limit. */
  TooManyKeys = 'TooManyKeys',
  /** The account's key file path is not safe to write. */
  UnsafePath = 'UnsafePath',
  /** The account's key file is too large. */
  FileTooLarge = 'FileTooLarge',
  /** The account is suspended on the server (its password is locked) and may not receive keys. */
  AccountSuspended = 'AccountSuspended',
  /** HostPanel could not ask the server at all: it no longer exists or has no complete agent settings. */
  AgentNotConfigured = 'AgentNotConfigured',
  /** The agent that answered was installed for another server record (its binding differs). */
  BindingMismatch = 'BindingMismatch',
}
