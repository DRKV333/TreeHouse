import timm
import torch
import onnx
import onnx.inliner
import onnx.tools
import onnx.tools.update_model_dims
import numpy as np
import onnxsim

def get_onnx_from_timm():
    resnet_torch = timm.create_model("resnet50.a1_in1k", pretrained=True, num_classes=0)
    resnet_torch.eval()

    torch_input = torch.rand((1, 3, 224, 224), dtype=torch.float32)
    return torch.onnx.dynamo_export(resnet_torch, torch_input).model_proto

def make_reshape_dynamic(model):
    for node in model.graph.node:
        if node.op_type == "Reshape":
            reshape_node = node
            break

    for node in model.graph.node:
        if node.output[0] == reshape_node.input[1]:
            reshape_const_node = node
            break

    reshape_shape = np.array(onnx.numpy_helper.to_array(reshape_const_node.attribute[0].t))
    reshape_shape[0] = 0

    new_shape = onnx.numpy_helper.from_array(reshape_shape)

    reshape_const_node.attribute[0].t.CopyFrom(new_shape)

def rename_input_output(model):
    rename_nodes(model, {
        model.graph.input[0].name: "data",
        model.graph.output[0].name: "features"
    })

def rename_nodes(model, renames):
    def replace_in_list(l):
        for i, name in enumerate(l):
            if name in renames:
                l[i] = renames[name]

    def replace_name(l):
        for value_info in l:
            if value_info.name in renames:
                value_info.name = renames[value_info.name]

    for node in model.graph.node:
        replace_in_list(node.input)
        replace_in_list(node.output)

    replace_name(model.graph.input)
    replace_name(model.graph.output)

    onnx.checker.check_model(model)

def make_input_output_dynamic(model):
    model.graph.ClearField("value_info")

    rightshape = onnx.tools.update_model_dims.update_inputs_outputs_dims(
        resnet_inline,
        input_dims={ "data": ["N", 3, 224, 224] },
        output_dims={ "features": ["N", 2048 ] }
    )
    
    return onnx.shape_inference.infer_shapes(rightshape)

resnet_fromtorch = get_onnx_from_timm()
resnet_inline = onnx.inliner.inline_local_functions(resnet_fromtorch)
make_reshape_dynamic(resnet_inline)
rename_input_output(resnet_inline)
resnet_rightshape = make_input_output_dynamic(resnet_inline)
resnet_opt, check = onnxsim.simplify(resnet_rightshape)

onnx.save(resnet_opt, "resnet_features.onnx")