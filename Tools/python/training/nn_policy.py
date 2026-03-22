from __future__ import annotations

import math
import random
from typing import Dict, Iterable, List, Sequence


def relu(values: Sequence[float]) -> List[float]:
    return [value if value > 0.0 else 0.0 for value in values]


def softmax(logits: Sequence[float], mask: Sequence[int] | None = None) -> List[float]:
    allowed = [
        index
        for index, value in enumerate(logits)
        if mask is None or (index < len(mask) and mask[index])
    ]
    if not allowed:
        return [0.0 for _ in logits]

    best = max(logits[index] for index in allowed)
    exps = [0.0 for _ in logits]
    total = 0.0
    for index in allowed:
        value = math.exp(logits[index] - best)
        exps[index] = value
        total += value

    if total <= 0.0:
        uniform = 1.0 / len(allowed)
        return [uniform if index in allowed else 0.0 for index in range(len(logits))]

    return [value / total for value in exps]


class TinyPolicyNetwork:
    def __init__(
        self,
        input_dim: int,
        hidden_dim: int,
        output_dim: int,
        seed: int = 7,
        hidden_dim2: int = 0,
    ) -> None:
        rng = random.Random(seed)
        self.input_dim = input_dim
        self.hidden_dim = hidden_dim
        self.hidden_dim2 = hidden_dim2
        self.output_dim = output_dim

        self.w1 = [[rng.uniform(-0.08, 0.08) for _ in range(input_dim)] for _ in range(hidden_dim)]
        self.b1 = [0.0 for _ in range(hidden_dim)]

        if hidden_dim2 > 0:
            self.w2 = [[rng.uniform(-0.08, 0.08) for _ in range(hidden_dim)] for _ in range(hidden_dim2)]
            self.b2 = [0.0 for _ in range(hidden_dim2)]
            output_input_dim = hidden_dim2
        else:
            self.w2 = []
            self.b2 = []
            output_input_dim = hidden_dim

        self.w_out = [[rng.uniform(-0.08, 0.08) for _ in range(output_input_dim)] for _ in range(output_dim)]
        self.b_out = [0.0 for _ in range(output_dim)]

    def forward(self, observation: Sequence[float]) -> Dict[str, List[float]]:
        hidden1_pre = [
            sum(weight * value for weight, value in zip(row, observation)) + bias
            for row, bias in zip(self.w1, self.b1)
        ]
        hidden1 = relu(hidden1_pre)

        if self.hidden_dim2 > 0:
            hidden2_pre = [
                sum(weight * value for weight, value in zip(row, hidden1)) + bias
                for row, bias in zip(self.w2, self.b2)
            ]
            hidden2 = relu(hidden2_pre)
            output_input = hidden2
        else:
            hidden2_pre = []
            hidden2 = []
            output_input = hidden1

        logits = [
            sum(weight * value for weight, value in zip(row, output_input)) + bias
            for row, bias in zip(self.w_out, self.b_out)
        ]
        return {
            "hidden1_pre": hidden1_pre,
            "hidden1": hidden1,
            "hidden2_pre": hidden2_pre,
            "hidden2": hidden2,
            "output_input": output_input,
            "logits": logits,
        }

    def train_step(
        self,
        observation: Sequence[float],
        target_action_id: int,
        action_mask: Sequence[int],
        learning_rate: float,
    ) -> float:
        cache = self.forward(observation)
        hidden1_pre = cache["hidden1_pre"]
        hidden1 = cache["hidden1"]
        hidden2_pre = cache["hidden2_pre"]
        hidden2 = cache["hidden2"]
        output_input = cache["output_input"]
        logits = cache["logits"]

        probabilities = softmax(logits, action_mask)
        target_prob = max(probabilities[target_action_id], 1e-8)
        loss = -math.log(target_prob)

        grad_logits = probabilities[:]
        grad_logits[target_action_id] -= 1.0
        for index in range(self.output_dim):
            if index >= len(action_mask) or not action_mask[index]:
                grad_logits[index] = 0.0

        grad_output_input = [0.0 for _ in range(len(output_input))]
        for out_index in range(self.output_dim):
            delta = grad_logits[out_index]
            if delta == 0.0:
                continue
            for input_index in range(len(output_input)):
                grad_output_input[input_index] += self.w_out[out_index][input_index] * delta
                self.w_out[out_index][input_index] -= learning_rate * delta * output_input[input_index]
            self.b_out[out_index] -= learning_rate * delta

        if self.hidden_dim2 > 0:
            grad_hidden2 = grad_output_input[:]
            for hidden_index in range(self.hidden_dim2):
                if hidden2_pre[hidden_index] <= 0.0:
                    grad_hidden2[hidden_index] = 0.0

            grad_hidden1 = [0.0 for _ in range(self.hidden_dim)]
            for hidden2_index in range(self.hidden_dim2):
                delta = grad_hidden2[hidden2_index]
                if delta == 0.0:
                    continue
                for hidden1_index in range(self.hidden_dim):
                    grad_hidden1[hidden1_index] += self.w2[hidden2_index][hidden1_index] * delta
                    self.w2[hidden2_index][hidden1_index] -= learning_rate * delta * hidden1[hidden1_index]
                self.b2[hidden2_index] -= learning_rate * delta
        else:
            grad_hidden1 = grad_output_input[:]

        for hidden_index in range(self.hidden_dim):
            if hidden1_pre[hidden_index] <= 0.0:
                grad_hidden1[hidden_index] = 0.0

        for hidden_index in range(self.hidden_dim):
            delta = grad_hidden1[hidden_index]
            if delta == 0.0:
                continue
            for input_index in range(self.input_dim):
                self.w1[hidden_index][input_index] -= learning_rate * delta * observation[input_index]
            self.b1[hidden_index] -= learning_rate * delta

        return loss

    def predict_action(self, observation: Sequence[float], action_mask: Sequence[int]) -> int | None:
        logits = self.forward(observation)["logits"]
        best_index = None
        best_score = None
        for index, allowed in enumerate(action_mask):
            if not allowed:
                continue
            score = logits[index]
            if best_score is None or score > best_score:
                best_score = score
                best_index = index
        return best_index

    def to_payload(self) -> Dict:
        return {
            "type": "tiny_mlp_behavior_clone",
            "input_dim": self.input_dim,
            "hidden_dim": self.hidden_dim,
            "hidden_dim2": self.hidden_dim2,
            "output_dim": self.output_dim,
            "w1": self.w1,
            "b1": self.b1,
            "w2": self.w2,
            "b2": self.b2,
            "w_out": self.w_out,
            "b_out": self.b_out,
        }

    @classmethod
    def from_payload(cls, payload: Dict) -> "TinyPolicyNetwork":
        hidden_dim2 = int(payload.get("hidden_dim2", 0))
        network = cls(
            input_dim=int(payload["input_dim"]),
            hidden_dim=int(payload["hidden_dim"]),
            hidden_dim2=hidden_dim2,
            output_dim=int(payload["output_dim"]),
        )
        network.w1 = [[float(value) for value in row] for row in payload["w1"]]
        network.b1 = [float(value) for value in payload["b1"]]

        if "w_out" in payload:
            network.w2 = [[float(value) for value in row] for row in payload["w2"]]
            network.b2 = [float(value) for value in payload["b2"]]
            network.w_out = [[float(value) for value in row] for row in payload["w_out"]]
            network.b_out = [float(value) for value in payload["b_out"]]
        else:
            network.w_out = [[float(value) for value in row] for row in payload["w2"]]
            network.b_out = [float(value) for value in payload["b2"]]
            network.w2 = []
            network.b2 = []
            network.hidden_dim2 = 0
        return network


def shuffled_indices(length: int, seed: int) -> Iterable[int]:
    indices = list(range(length))
    random.Random(seed).shuffle(indices)
    return indices
