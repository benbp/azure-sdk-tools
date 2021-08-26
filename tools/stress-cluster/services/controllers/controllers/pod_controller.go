/*
Copyright 2021 Microsoft.com.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

package controllers

import (
	"context"
	"fmt"

	"github.com/go-logr/logr"
	corev1 "k8s.io/api/core/v1"
	"k8s.io/apimachinery/pkg/runtime"
	ctrl "sigs.k8s.io/controller-runtime"
	"sigs.k8s.io/controller-runtime/pkg/client"
	"sigs.k8s.io/controller-runtime/pkg/log"

	chaosMesh "github.com/chaos-mesh/chaos-mesh/api/v1alpha1"
)

// PodReconciler reconciles a Pod object
type PodReconciler struct {
	client.Client
	Scheme *runtime.Scheme
	log    logr.Logger
}

const chaosAnnotation = "stress/chaos/started"
const chaosLabelSelector = "testInstance"

//+kubebuilder:rbac:groups=core,resources=pods,verbs=get;list;watch;create;update;patch;delete
//+kubebuilder:rbac:groups=core,resources=pods/status,verbs=get;update;patch
//+kubebuilder:rbac:groups=core,resources=pods/finalizers,verbs=update

// Reconcile is part of the main kubernetes reconciliation loop which aims to
// move the current state of the cluster closer to the desired state.
func (r *PodReconciler) Reconcile(ctx context.Context, req ctrl.Request) (ctrl.Result, error) {
	r.log = log.FromContext(ctx).WithName("stress_watcher")

	pod := corev1.Pod{}
	if err := r.Client.Get(ctx, req.NamespacedName, &pod); err != nil {
		r.log.Error(err, "Failed to get pod resource")
		return ctrl.Result{}, nil
	}

	r.log = r.log.WithValues("Pod", fmt.Sprintf("%s/%s", pod.Namespace, pod.Name))

	err := r.handleChaos(ctx, &pod)
	if err != nil {
		return ctrl.Result{}, nil
	}

	return ctrl.Result{}, nil
}

func (r *PodReconciler) handleChaos(ctx context.Context, pod *corev1.Pod) error {
	networkChaosList := chaosMesh.NetworkChaosList{}
	listOpt := client.ListOptions{Namespace: pod.Namespace}
	if err := r.Client.List(ctx, &networkChaosList, &listOpt); err != nil {
		r.log.Error(err, "Failed to get network chaos resources")
		return err
	}

	labels := pod.GetLabels()
	annotations := pod.GetAnnotations()

	if chaosStarted, ok := annotations[chaosAnnotation]; ok && chaosStarted == "true" {
		r.log.Info("Pod has already been enabled for chaos")
	} else if chaos, ok := labels["chaos"]; ok && chaos == "true" {
		r.log.Info("Enabling chaos for pod")
		testInstance, ok := labels[chaosLabelSelector]
		if !ok {
			return fmt.Errorf("Chaos enabled pod is missing %s label", chaosLabelSelector)
		}
		for _, res := range networkChaosList.Items {
			if chaosInstance, ok := res.Spec.Selector.LabelSelectors[chaosLabelSelector]; ok {
				if chaosInstance == testInstance {
					r.log.Info("Found match!", "TestInstance", chaosInstance)
				}
			}
		}
	} else {
		r.log.Info("Ignoring pod for chaos")
	}

	return nil
}

func (r *PodReconciler) findMatchingChaosResources(
	ctx context.Context,
	pod *corev1.Pod,
	chaosResources chaosMesh.GenericChaosList,
) []chaosMesh.GenericChaos {
	return []chaosMesh.GenericChaos{}
}

// SetupWithManager sets up the controller with the Manager.
func (r *PodReconciler) SetupWithManager(mgr ctrl.Manager) error {
	return ctrl.NewControllerManagedBy(mgr).
		For(&corev1.Pod{}).
		Complete(r)
}
