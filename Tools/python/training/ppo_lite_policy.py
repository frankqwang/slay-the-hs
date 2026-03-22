from __future__ import annotations

import math
import random
from typing import Dict, List, Sequence

from training.nn_policy import relu, softmax, TinyPolicyNetwork


class TinyActorCritic:
    def __init__(self, input_dim: int, hidden_dim: int, output_dim: int, seed: int = 11) -> None:
        rng = random.Random(seed)
        self.input_dim = input_dim
        self.hidden_dim = hidden_dim
        self.output_dim = output_dim
        self.w1 = [[rng.uniform(-0.08, 0.08) for _ in range(input_dim)] for _ in range(hidden_dim)]
        self.b1 = [0.0 for _ in range(hidden_dim)]
        self.w_policy = [[rng.uniform(-0.08, 0.08) for _ in range(hidden_dim)] for _ in range(output_dim)]
        self.b_policy = [0.0 for _ in range(output_dim)]
        self.w_value = [rng.uniform(-0.08, 0.08) for _ in range(hidden_dim)]
        self.b_value = 0.0

    def forward(self, observation: Sequence[float]) -> Dict[str, List[float] | float]:
        hidden_pre = [
            sum(weight * value for weight, value in zip(row, observation)) + bias
            for row, bias in zip(self.w1, self.b1)
        ]
        hidden = relu(hidden_pre)
        logits = [
            sum(weight * value for weight, value in zip(row, hidden)) + bias
            for row, bias in zip(self.w_policy, self.b_policy)
        ]
        value = sum(weight * value for weight, value in zip(self.w_value, hidden)) + self.b_value
        return {
            "hidden_pre": hidden_pre,
            "hidden": hidden,
            "logits": logits,
            "value": value,
        }

    def action_distribution(self, observation: Sequence[float], action_mask: Sequence[int]) -> tuple[List[float], float]:
        cache = self.forward(observation)
        probabilities = softmax(cache["logits"], action_mask)  # type: ignore[arg-type]
        return probabilities, float(cache["value"])

    def sample_action(self, observation: Sequence[float], action_mask: Sequence[int], rng: random.Random) -> tuple[int | None, float, float]:
        probabilities, value = self.action_distribution(observation, action_mask)
        roll = rng.random()
        total = 0.0
        for index, probability in enumerate(probabilities):
            if probability <= 0.0:
                continue
            total += probability
            if roll <= total:
                return index, max(probability, 1e-8), value

        best_index = None
        best_prob = 0.0
        for index, probability in enumerate(probabilities):
            if probability > best_prob:
                best_prob = probability
                best_index = index
        return best_index, max(best_prob, 1e-8), value

    def predict_action(self, observation: Sequence[float], action_mask: Sequence[int]) -> int | None:
        probabilities, _ = self.action_distribution(observation, action_mask)
        best_index = None
        best_prob = None
        for index, probability in enumerate(probabilities):
            if probability <= 0.0:
                continue
            if best_prob is None or probability > best_prob:
                best_prob = probability
                best_index = index
        return best_index

    def ppo_update_step(
        self,
        observation: Sequence[float],
        action_id: int,
        action_mask: Sequence[int],
        old_probability: float,
        advantage: float,
        target_return: float,
        *,
        learning_rate: float,
        clip_epsilon: float,
        value_coef: float,
    ) -> Dict[str, float]:
        cache = self.forward(observation)
        hidden_pre = cache["hidden_pre"]  # type: ignore[assignment]
        hidden = cache["hidden"]  # type: ignore[assignment]
        logits = cache["logits"]  # type: ignore[assignment]
        value = float(cache["value"])
        probabilities = softmax(logits, action_mask)
        current_probability = max(probabilities[action_id], 1e-8)
        old_probability = max(old_probability, 1e-8)
        ratio = current_probability / old_probability

        use_clipped = (advantage >= 0.0 and ratio > 1.0 + clip_epsilon) or (advantage < 0.0 and ratio < 1.0 - clip_epsilon)
        policy_scale = 0.0 if use_clipped else (-advantage * ratio)

        grad_logits = [0.0 for _ in range(self.output_dim)]
        if policy_scale != 0.0:
            for index in range(self.output_dim):
                if index >= len(action_mask) or not action_mask[index]:
                    continue
                indicator = 1.0 if index == action_id else 0.0
                grad_logits[index] = policy_scale * (indicator - probabilities[index]) * -1.0

        value_error = value - target_return
        grad_value = value_coef * value_error

        grad_hidden = [0.0 for _ in range(self.hidden_dim)]

        for out_index in range(self.output_dim):
            delta = grad_logits[out_index]
            if delta == 0.0:
                continue
            for hidden_index in range(self.hidden_dim):
                grad_hidden[hidden_index] += self.w_policy[out_index][hidden_index] * delta
                self.w_policy[out_index][hidden_index] -= learning_rate * delta * hidden[hidden_index]
            self.b_policy[out_index] -= learning_rate * delta

        for hidden_index in range(self.hidden_dim):
            grad_hidden[hidden_index] += self.w_value[hidden_index] * grad_value
            self.w_value[hidden_index] -= learning_rate * grad_value * hidden[hidden_index]
        self.b_value -= learning_rate * grad_value

        for hidden_index in range(self.hidden_dim):
            if hidden_pre[hidden_index] <= 0.0:
                grad_hidden[hidden_index] = 0.0

        for hidden_index in range(self.hidden_dim):
            delta = grad_hidden[hidden_index]
            if delta == 0.0:
                continue
            for input_index in range(self.input_dim):
                self.w1[hidden_index][input_index] -= learning_rate * delta * observation[input_index]
            self.b1[hidden_index] -= learning_rate * delta

        policy_loss = -min(ratio * advantage, max(min(ratio, 1.0 + clip_epsilon), 1.0 - clip_epsilon) * advantage)
        value_loss = 0.5 * (target_return - value) * (target_return - value)
        return {
            "policy_loss": policy_loss,
            "value_loss": value_loss,
            "ratio": ratio,
        }

    def to_payload(self) -> Dict:
        return {
            "type": "tiny_actor_critic_ppo_lite",
            "input_dim": self.input_dim,
            "hidden_dim": self.hidden_dim,
            "output_dim": self.output_dim,
            "w1": self.w1,
            "b1": self.b1,
            "w_policy": self.w_policy,
            "b_policy": self.b_policy,
            "w_value": self.w_value,
            "b_value": self.b_value,
        }

    @classmethod
    def from_payload(cls, payload: Dict) -> "TinyActorCritic":
        network = cls(
            input_dim=int(payload["input_dim"]),
            hidden_dim=int(payload["hidden_dim"]),
            output_dim=int(payload["output_dim"]),
        )
        network.w1 = [[float(value) for value in row] for row in payload["w1"]]
        network.b1 = [float(value) for value in payload["b1"]]
        network.w_policy = [[float(value) for value in row] for row in payload["w_policy"]]
        network.b_policy = [float(value) for value in payload["b_policy"]]
        network.w_value = [float(value) for value in payload["w_value"]]
        network.b_value = float(payload["b_value"])
        return network

    @classmethod
    def from_bc_policy(cls, policy: TinyPolicyNetwork) -> "TinyActorCritic":
        network = cls(policy.input_dim, policy.hidden_dim, policy.output_dim)
        network.w1 = [row[:] for row in policy.w1]
        network.b1 = policy.b1[:]
        network.w_policy = [row[:] for row in policy.w2]
        network.b_policy = policy.b2[:]
        network.w_value = [0.0 for _ in range(policy.hidden_dim)]
        network.b_value = 0.0
        return network


def compute_gae(rewards: List[float], values: List[float], dones: List[bool], gamma: float, lam: float) -> tuple[List[float], List[float]]:
    advantages = [0.0 for _ in rewards]
    returns = [0.0 for _ in rewards]
    next_advantage = 0.0
    next_value = 0.0

    for index in range(len(rewards) - 1, -1, -1):
        mask = 0.0 if dones[index] else 1.0
        delta = rewards[index] + gamma * next_value * mask - values[index]
        next_advantage = delta + gamma * lam * mask * next_advantage
        advantages[index] = next_advantage
        returns[index] = advantages[index] + values[index]
        next_value = values[index]

    return advantages, returns


def normalize(values: List[float]) -> List[float]:
    if not values:
        return values
    mean = sum(values) / len(values)
    variance = sum((value - mean) * (value - mean) for value in values) / len(values)
    std = math.sqrt(max(variance, 1e-8))
    return [(value - mean) / std for value in values]
