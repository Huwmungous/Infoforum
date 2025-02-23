export interface DeepseekResponse {
    model: string;
    created_at: string;  // You could also convert this to a Date if needed.
    response: string;
    done: boolean;
    // These properties are only present on the final response
    done_reason?: string;
    context?: number[];
    total_duration?: number;
    load_duration?: number;
    prompt_eval_count?: number;
    prompt_eval_duration?: number;
    eval_count?: number;
    eval_duration?: number;
  }
  